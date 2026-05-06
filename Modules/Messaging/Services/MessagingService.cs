using Microsoft.EntityFrameworkCore;
using Portlink.Api.Data;
using Portlink.Api.Entities;
using Portlink.Api.Modules.Common.Dtos;
using Portlink.Api.Modules.Messaging.Dtos;
using Portlink.Api.Modules.Messaging.Interfaces;

namespace Portlink.Api.Modules.Messaging;

public class MessagingService : IMessagingService
{
    private const string AgentRole = "agent";
    private const string SubcontractorRole = "subcontractor";
    private const int MaxMessageBodyLength = 4000;
    private const string DeletedMessagePlaceholder = "Bu mesaj silindi";

    private readonly AppDbContext _db;

    public MessagingService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ConversationResponse> StartConversationAsync(Guid userId, StartConversationRequest request)
    {
        var currentUser = await GetMessagingUserAsync(userId);
        var resolvedOtherUserId = await ResolveOtherUserIdAsync(currentUser, request.OtherUserId);
        var otherUser = await GetMessagingUserAsync(resolvedOtherUserId);

        ValidateRolePair(currentUser.Role, otherUser.Role);

        var job = await _db.JobListings
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == request.JobListingId)
            ?? throw new KeyNotFoundException("Ä°lan bulunamadÄ±.");

        var participantIds = ResolveParticipantIds(currentUser, otherUser);
        await EnsureConversationCanBeStartedAsync(currentUser, otherUser, participantIds, job);

        var existing = await _db.Conversations
            .FirstOrDefaultAsync(c =>
                c.AgentId == participantIds.AgentProfileId &&
                c.SubcontractorId == participantIds.SubcontractorProfileId &&
                c.JobListingId == request.JobListingId);

        if (existing != null)
        {
            return await BuildConversationResponseAsync(existing.Id, currentUser);
        }

        var now = DateTime.UtcNow;
        var conversation = new Conversation
        {
            AgentId = participantIds.AgentProfileId,
            SubcontractorId = participantIds.SubcontractorProfileId,
            JobListingId = request.JobListingId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Conversations.Add(conversation);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _db.Entry(conversation).State = EntityState.Detached;

            var concurrentConversation = await _db.Conversations
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.AgentId == participantIds.AgentProfileId &&
                    c.SubcontractorId == participantIds.SubcontractorProfileId &&
                    c.JobListingId == request.JobListingId);

            if (concurrentConversation != null)
            {
                return await BuildConversationResponseAsync(concurrentConversation.Id, currentUser);
            }

            throw;
        }

        return await BuildConversationResponseAsync(conversation.Id, currentUser);
    }

    public async Task<List<ConversationResponse>> GetConversationsAsync(Guid userId)
    {
        var currentUser = await GetMessagingUserAsync(userId);

        var conversations = await _db.Conversations
            .AsNoTracking()
            .Include(c => c.JobListing)
            .Include(c => c.Agent)
            .Include(c => c.Subcontractor)
            .Where(c =>
                (currentUser.AgentProfileId.HasValue && c.AgentId == currentUser.AgentProfileId.Value) ||
                (currentUser.SubcontractorProfileId.HasValue && c.SubcontractorId == currentUser.SubcontractorProfileId.Value))
            .OrderByDescending(c => c.LastMessageAt ?? c.UpdatedAt)
            .ThenByDescending(c => c.UpdatedAt)
            .ToListAsync();

        if (conversations.Count == 0)
        {
            return new List<ConversationResponse>();
        }

        var participantUserIds = conversations
            .SelectMany(c => new[] { c.Agent.UserId, c.Subcontractor.UserId })
            .Distinct()
            .ToList();

        var users = await _db.Users
            .AsNoTracking()
            .Where(u => participantUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var conversationIds = conversations.Select(c => c.Id).ToList();
        var states = await _db.ConversationUserStates
            .AsNoTracking()
            .Where(s => s.UserId == userId && conversationIds.Contains(s.ConversationId))
            .ToDictionaryAsync(s => s.ConversationId, s => s.LastClearedAt);

        var visibleMessages = await _db.ConversationMessages
            .AsNoTracking()
            .Where(m => conversationIds.Contains(m.ConversationId))
            .Where(m => !m.Deletions.Any(d => d.UserId == userId))
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        var visibleMessagesByConversation = visibleMessages
            .Where(m => !states.TryGetValue(m.ConversationId, out var lastClearedAt) || m.CreatedAt > lastClearedAt)
            .GroupBy(m => m.ConversationId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return conversations.Select(conversation =>
        {
            visibleMessagesByConversation.TryGetValue(conversation.Id, out var conversationMessages);
            var lastVisibleMessage = conversationMessages?.FirstOrDefault();
            var unreadCount = conversationMessages?.Count(m => m.SenderId != userId && m.ReadAt == null) ?? 0;

            return MapConversationResponse(conversation, userId, users, lastVisibleMessage, unreadCount);
        }).ToList();
    }

    public async Task<PaginatedResponse<ConversationMessageResponse>> GetMessagesAsync(Guid userId, Guid conversationId, int page, int pageSize)
    {
        var currentUser = await GetMessagingUserAsync(userId);
        var conversation = await GetAuthorizedConversationAsync(currentUser, conversationId);
        var lastClearedAt = await GetLastClearedAtAsync(conversation.Id, userId);

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 30 : pageSize > 100 ? 100 : pageSize;

        var query = BuildVisibleMessagesQuery(conversation.Id, userId, lastClearedAt)
            .AsNoTracking()
            .Include(m => m.Sender);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        return new PaginatedResponse<ConversationMessageResponse>
        {
            Items = items.Select(m => MapMessageResponse(m, currentUser)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ConversationMessageResponse> SendMessageAsync(Guid userId, Guid conversationId, SendConversationMessageRequest request)
    {
        var currentUser = await GetMessagingUserAsync(userId);
        var conversation = await GetAuthorizedConversationAsync(currentUser, conversationId);
        var body = NormalizeMessageBody(request.Body);
        var now = DateTime.UtcNow;
        var recipientUserId = await GetOtherUserIdAsync(conversation, userId);
        var jobTitle = await _db.JobListings
            .Where(j => j.Id == conversation.JobListingId)
            .Select(j => j.Title)
            .FirstOrDefaultAsync() ?? "Ä°lan";

        var message = new ConversationMessage
        {
            ConversationId = conversation.Id,
            SenderId = userId,
            Body = body,
            CreatedAt = now
        };

        _db.ConversationMessages.Add(message);
        _db.Notifications.Add(new Notification
        {
            UserId = recipientUserId,
            Type = "NEW_MESSAGE",
            Title = "Yeni Mesaj",
            Body = $"{jobTitle} iÃ§in yeni bir mesajÄ±nÄ±z var.",
            Data = System.Text.Json.JsonSerializer.Serialize(new { conversationId = conversation.Id })
        });

        await RefreshConversationMetadataAsync(conversation, now);
        await _db.SaveChangesAsync();

        message.Sender = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
        return MapMessageResponse(message, currentUser);
    }

    public async Task<ConversationMessageResponse> EditMessageAsync(Guid userId, Guid conversationId, Guid messageId, EditConversationMessageRequest request)
    {
        var currentUser = await GetMessagingUserAsync(userId);
        var conversation = await GetAuthorizedConversationAsync(currentUser, conversationId);
        var message = await GetConversationMessageAsync(conversation.Id, messageId);

        if (message.SenderId != userId)
        {
            throw new UnauthorizedAccessException("YalnÄ±zca kendi mesajÄ±nÄ±zÄ± dÃ¼zenleyebilirsiniz.");
        }

        if (message.IsDeletedForEveryone)
        {
            throw new InvalidOperationException("SilinmiÅŸ bir mesaj dÃ¼zenlenemez.");
        }

        var body = NormalizeMessageBody(request.Body);
        if (message.Body != body)
        {
            message.Body = body;
            message.IsEdited = true;
            message.EditedAt = DateTime.UtcNow;
            await RefreshConversationMetadataAsync(conversation);
            await _db.SaveChangesAsync();
        }

        return MapMessageResponse(message, currentUser);
    }

    public async Task DeleteMessageForMeAsync(Guid userId, Guid conversationId, Guid messageId)
    {
        var currentUser = await GetMessagingUserAsync(userId);
        var conversation = await GetAuthorizedConversationAsync(currentUser, conversationId);
        var message = await GetConversationMessageAsync(conversation.Id, messageId, includeSender: false);

        var alreadyDeleted = await _db.ConversationMessageDeletions
            .AnyAsync(d => d.MessageId == message.Id && d.UserId == userId);

        if (alreadyDeleted)
        {
            return;
        }

        _db.ConversationMessageDeletions.Add(new ConversationMessageDeletion
        {
            MessageId = message.Id,
            UserId = userId,
            DeletedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    public async Task DeleteMessageForEveryoneAsync(Guid userId, Guid conversationId, Guid messageId)
    {
        var currentUser = await GetMessagingUserAsync(userId);
        var conversation = await GetAuthorizedConversationAsync(currentUser, conversationId);
        var message = await GetConversationMessageAsync(conversation.Id, messageId);

        if (message.SenderId != userId)
        {
            throw new UnauthorizedAccessException("BaÅŸkasÄ±na ait mesajÄ± herkes iÃ§in silemezsiniz.");
        }

        if (message.IsDeletedForEveryone)
        {
            return;
        }

        message.IsDeletedForEveryone = true;
        message.DeletedAt = DateTime.UtcNow;
        message.DeletedByUserId = userId;
        await RefreshConversationMetadataAsync(conversation);
        await _db.SaveChangesAsync();
    }

    public async Task ClearConversationHistoryAsync(Guid userId, Guid conversationId)
    {
        var currentUser = await GetMessagingUserAsync(userId);
        var conversation = await GetAuthorizedConversationAsync(currentUser, conversationId);
        var now = DateTime.UtcNow;

        var state = await _db.ConversationUserStates
            .FirstOrDefaultAsync(s => s.ConversationId == conversation.Id && s.UserId == userId);

        if (state == null)
        {
            state = new ConversationUserState
            {
                ConversationId = conversation.Id,
                UserId = userId,
                LastClearedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.ConversationUserStates.Add(state);
        }
        else
        {
            state.LastClearedAt = now;
            state.UpdatedAt = now;
        }

        await _db.SaveChangesAsync();
    }

    public async Task MarkConversationReadAsync(Guid userId, Guid conversationId)
    {
        var currentUser = await GetMessagingUserAsync(userId);
        var conversation = await GetAuthorizedConversationAsync(currentUser, conversationId);
        var lastClearedAt = await GetLastClearedAtAsync(conversation.Id, userId);

        var unreadMessages = await BuildVisibleMessagesQuery(conversation.Id, userId, lastClearedAt)
            .Where(m => m.SenderId != userId && m.ReadAt == null)
            .ToListAsync();

        if (unreadMessages.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        unreadMessages.ForEach(m => m.ReadAt = now);
        await _db.SaveChangesAsync();
    }

    private async Task<Guid> ResolveOtherUserIdAsync(MessagingUser currentUser, Guid requestedOtherUserId)
    {
        var existingUserId = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == requestedOtherUserId && u.IsActive)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync();

        if (existingUserId.HasValue)
        {
            return existingUserId.Value;
        }

        if (currentUser.Role == SubcontractorRole)
        {
            var agentUserId = await _db.AgentProfiles
                .AsNoTracking()
                .Where(a => a.Id == requestedOtherUserId)
                .Select(a => (Guid?)a.UserId)
                .FirstOrDefaultAsync();

            if (agentUserId.HasValue)
            {
                return agentUserId.Value;
            }
        }
        else if (currentUser.Role == AgentRole)
        {
            var subcontractorUserId = await _db.SubcontractorProfiles
                .AsNoTracking()
                .Where(s => s.Id == requestedOtherUserId)
                .Select(s => (Guid?)s.UserId)
                .FirstOrDefaultAsync();

            if (subcontractorUserId.HasValue)
            {
                return subcontractorUserId.Value;
            }
        }

        throw new KeyNotFoundException("KarÃ…Å¸Ã„Â± kullanÃ„Â±cÃ„Â± bulunamadÃ„Â±.");
    }

    private async Task<ConversationResponse> BuildConversationResponseAsync(Guid conversationId, MessagingUser currentUser)
    {
        var conversation = await _db.Conversations
            .AsNoTracking()
            .Include(c => c.JobListing)
            .Include(c => c.Agent)
            .Include(c => c.Subcontractor)
            .FirstAsync(c => c.Id == conversationId);

        var users = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == conversation.Agent.UserId || u.Id == conversation.Subcontractor.UserId)
            .ToDictionaryAsync(u => u.Id);

        var lastClearedAt = await GetLastClearedAtAsync(conversation.Id, currentUser.UserId);
        var conversationMessages = await BuildVisibleMessagesQuery(conversation.Id, currentUser.UserId, lastClearedAt)
            .AsNoTracking()
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        var lastVisibleMessage = conversationMessages.FirstOrDefault();
        var unreadCount = conversationMessages.Count(m => m.SenderId != currentUser.UserId && m.ReadAt == null);

        return MapConversationResponse(conversation, currentUser.UserId, users, lastVisibleMessage, unreadCount);
    }

    private async Task<Conversation> GetAuthorizedConversationAsync(MessagingUser currentUser, Guid conversationId)
    {
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId)
            ?? throw new KeyNotFoundException("KonuÅŸma bulunamadÄ±.");

        var isParticipant =
            (currentUser.AgentProfileId.HasValue && conversation.AgentId == currentUser.AgentProfileId.Value) ||
            (currentUser.SubcontractorProfileId.HasValue && conversation.SubcontractorId == currentUser.SubcontractorProfileId.Value);

        if (!isParticipant)
        {
            throw new UnauthorizedAccessException("Bu konuÅŸmaya eriÅŸim yetkiniz yok.");
        }

        return conversation;
    }

    private async Task<ConversationMessage> GetConversationMessageAsync(Guid conversationId, Guid messageId, bool includeSender = true)
    {
        IQueryable<ConversationMessage> query = _db.ConversationMessages;
        if (includeSender)
        {
            query = query.Include(m => m.Sender);
        }

        var message = await query.FirstOrDefaultAsync(m => m.Id == messageId)
            ?? throw new KeyNotFoundException("Mesaj bulunamadÄ±.");

        if (message.ConversationId != conversationId)
        {
            throw new InvalidOperationException("Mesaj belirtilen konuÅŸmaya ait deÄŸil.");
        }

        return message;
    }

    private async Task<DateTime?> GetLastClearedAtAsync(Guid conversationId, Guid userId)
    {
        return await _db.ConversationUserStates
            .AsNoTracking()
            .Where(s => s.ConversationId == conversationId && s.UserId == userId)
            .Select(s => (DateTime?)s.LastClearedAt)
            .FirstOrDefaultAsync();
    }

    private IQueryable<ConversationMessage> BuildVisibleMessagesQuery(Guid conversationId, Guid userId, DateTime? lastClearedAt)
    {
        var query = _db.ConversationMessages
            .Where(m => m.ConversationId == conversationId)
            .Where(m => !m.Deletions.Any(d => d.UserId == userId));

        if (lastClearedAt.HasValue)
        {
            query = query.Where(m => m.CreatedAt > lastClearedAt.Value);
        }

        return query;
    }

    private async Task RefreshConversationMetadataAsync(Conversation conversation, DateTime? updatedAt = null)
    {
        var lastMessage = await _db.ConversationMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversation.Id)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync();

        conversation.LastMessageAt = lastMessage?.CreatedAt;
        conversation.LastMessagePreview = lastMessage == null ? null : BuildPreview(lastMessage);
        conversation.UpdatedAt = updatedAt ?? DateTime.UtcNow;
    }

    private static string NormalizeMessageBody(string? body)
    {
        var normalized = body?.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Mesaj iÃ§eriÄŸi boÅŸ olamaz.");
        }

        if (normalized.Length > MaxMessageBodyLength)
        {
            throw new InvalidOperationException($"Mesaj iÃ§eriÄŸi en fazla {MaxMessageBodyLength} karakter olabilir.");
        }

        return normalized;
    }

    private async Task<MessagingUser> GetMessagingUserAsync(Guid userId)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive)
            ?? throw new KeyNotFoundException("KullanÄ±cÄ± bulunamadÄ±.");

        Guid? agentProfileId = null;
        Guid? subcontractorProfileId = null;

        if (user.Role == AgentRole)
        {
            agentProfileId = await _db.AgentProfiles
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .Select(a => (Guid?)a.Id)
                .FirstOrDefaultAsync();
        }
        else if (user.Role == SubcontractorRole)
        {
            subcontractorProfileId = await _db.SubcontractorProfiles
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .Select(s => (Guid?)s.Id)
                .FirstOrDefaultAsync();
        }

        if (user.Role == AgentRole && !agentProfileId.HasValue)
        {
            throw new UnauthorizedAccessException("Acente profili bulunamadÄ±.");
        }

        if (user.Role == SubcontractorRole && !subcontractorProfileId.HasValue)
        {
            throw new UnauthorizedAccessException("TaÅŸeron profili bulunamadÄ±.");
        }

        if (user.Role != AgentRole && user.Role != SubcontractorRole)
        {
            throw new UnauthorizedAccessException("Bu hesap mesajlaÅŸma iÃ§in yetkili deÄŸil.");
        }

        return new MessagingUser
        {
            UserId = user.Id,
            Role = user.Role,
            AgentProfileId = agentProfileId,
            SubcontractorProfileId = subcontractorProfileId
        };
    }

    private static void ValidateRolePair(string currentRole, string otherRole)
    {
        var allowed =
            (currentRole == AgentRole && otherRole == SubcontractorRole) ||
            (currentRole == SubcontractorRole && otherRole == AgentRole);

        if (!allowed)
        {
            throw new InvalidOperationException("Bu sÃ¼rÃ¼mde yalnÄ±zca acente ve taÅŸeron arasÄ±nda mesajlaÅŸma desteklenir.");
        }
    }

    private static ParticipantIds ResolveParticipantIds(MessagingUser currentUser, MessagingUser otherUser)
    {
        if (currentUser.Role == AgentRole)
        {
            return new ParticipantIds(currentUser.AgentProfileId!.Value, otherUser.SubcontractorProfileId!.Value);
        }

        return new ParticipantIds(otherUser.AgentProfileId!.Value, currentUser.SubcontractorProfileId!.Value);
    }

    private async Task EnsureConversationCanBeStartedAsync(
        MessagingUser currentUser,
        MessagingUser otherUser,
        ParticipantIds participantIds,
        JobListing job)
    {
        if (job.AgentId != participantIds.AgentProfileId)
        {
            throw new UnauthorizedAccessException("Bu ilan iÃ§in konuÅŸma baÅŸlatma yetkiniz yok.");
        }

        var hasBusinessRelation = await _db.Offers.AnyAsync(o =>
                                     o.JobId == job.Id && o.SubcontractorId == participantIds.SubcontractorProfileId) ||
                                 await _db.AssignedJobs.AnyAsync(a =>
                                     a.JobId == job.Id && a.SubcontractorId == participantIds.SubcontractorProfileId);

        if (!hasBusinessRelation)
        {
            throw new InvalidOperationException("KonuÅŸma baÅŸlatmak iÃ§in ilanla iliÅŸkili teklif veya aktif iÅŸ kaydÄ± bulunmalÄ±dÄ±r.");
        }

        if (currentUser.Role == AgentRole && currentUser.AgentProfileId != participantIds.AgentProfileId)
        {
            throw new UnauthorizedAccessException("Bu ilan iÃ§in konuÅŸma baÅŸlatma yetkiniz yok.");
        }

        if (currentUser.Role == SubcontractorRole)
        {
            if (currentUser.SubcontractorProfileId != participantIds.SubcontractorProfileId ||
                otherUser.AgentProfileId != participantIds.AgentProfileId)
            {
                throw new UnauthorizedAccessException("Bu ilan iÃ§in konuÅŸma baÅŸlatma yetkiniz yok.");
            }
        }
    }

    private async Task<Guid> GetOtherUserIdAsync(Conversation conversation, Guid currentUserId)
    {
        var otherProfileUserId = await _db.AgentProfiles
            .Where(a => a.Id == conversation.AgentId)
            .Select(a => a.UserId)
            .FirstAsync();

        if (otherProfileUserId != currentUserId)
        {
            return otherProfileUserId;
        }

        return await _db.SubcontractorProfiles
            .Where(s => s.Id == conversation.SubcontractorId)
            .Select(s => s.UserId)
            .FirstAsync();
    }

    private static ConversationResponse MapConversationResponse(
        Conversation conversation,
        Guid currentUserId,
        IReadOnlyDictionary<Guid, User> users,
        ConversationMessage? lastVisibleMessage,
        int unreadCount)
    {
        var otherUserId = conversation.Agent.UserId == currentUserId
            ? conversation.Subcontractor.UserId
            : conversation.Agent.UserId;

        if (!users.TryGetValue(otherUserId, out var otherUser))
        {
            throw new KeyNotFoundException("KarÅŸÄ± kullanÄ±cÄ± bulunamadÄ±.");
        }

        var isOtherAgent = otherUser.Role == AgentRole;

        return new ConversationResponse
        {
            Id = conversation.Id,
            JobListingId = conversation.JobListingId,
            JobTitle = conversation.JobListing?.Title ?? string.Empty,
            OtherUserId = otherUserId,
            OtherRole = otherUser.Role,
            OtherCompanyName = isOtherAgent ? conversation.Agent.CompanyName : conversation.Subcontractor.CompanyName,
            OtherFullName = isOtherAgent ? conversation.Agent.FullName : conversation.Subcontractor.FullName,
            LastMessagePreview = lastVisibleMessage == null ? null : BuildPreview(lastVisibleMessage),
            LastMessageAt = lastVisibleMessage?.CreatedAt,
            UnreadCount = unreadCount,
            CreatedAt = conversation.CreatedAt,
            UpdatedAt = conversation.UpdatedAt
        };
    }

    private static ConversationMessageResponse MapMessageResponse(ConversationMessage message, MessagingUser currentUser)
    {
        return new ConversationMessageResponse
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            SenderRole = message.Sender.Role,
            Body = message.IsDeletedForEveryone ? DeletedMessagePlaceholder : message.Body,
            CreatedAt = message.CreatedAt,
            ReadAt = message.ReadAt,
            IsOwnMessage = message.SenderId == currentUser.UserId,
            IsEdited = message.IsEdited,
            EditedAt = message.EditedAt,
            IsDeletedForEveryone = message.IsDeletedForEveryone,
            DeletedAt = message.DeletedAt,
            DeletedByUserId = message.DeletedByUserId
        };
    }

    private static string BuildPreview(ConversationMessage message)
    {
        var body = message.IsDeletedForEveryone ? DeletedMessagePlaceholder : message.Body;
        return body.Length <= 300 ? body : body[..300];
    }

    private sealed class MessagingUser
    {
        public Guid UserId { get; init; }
        public string Role { get; init; } = string.Empty;
        public Guid? AgentProfileId { get; init; }
        public Guid? SubcontractorProfileId { get; init; }
    }

    private readonly record struct ParticipantIds(Guid AgentProfileId, Guid SubcontractorProfileId);
}
