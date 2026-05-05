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

    private readonly AppDbContext _db;

    public MessagingService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ConversationResponse> StartConversationAsync(Guid userId, StartConversationRequest request)
    {
        var currentUser = await GetMessagingUserAsync(userId);
        var otherUser = await GetMessagingUserAsync(request.OtherUserId);

        ValidateRolePair(currentUser.Role, otherUser.Role);

        var job = await _db.JobListings
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == request.JobListingId)
            ?? throw new KeyNotFoundException("İlan bulunamadı.");

        var participantIds = ResolveParticipantIds(currentUser, otherUser);
        await EnsureConversationCanBeStartedAsync(currentUser, otherUser, participantIds, job);

        var existing = await _db.Conversations
            .FirstOrDefaultAsync(c =>
                c.AgentId == participantIds.AgentProfileId &&
                c.SubcontractorId == participantIds.SubcontractorProfileId &&
                c.JobListingId == request.JobListingId);

        if (existing != null)
        {
            return await BuildConversationResponseAsync(existing.Id, userId);
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
                return await BuildConversationResponseAsync(concurrentConversation.Id, userId);
            }

            throw;
        }

        return await BuildConversationResponseAsync(conversation.Id, userId);
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

        var participantUserIds = conversations
            .SelectMany(c => new[] { c.Agent.UserId, c.Subcontractor.UserId })
            .Distinct()
            .ToList();

        var users = await _db.Users
            .AsNoTracking()
            .Where(u => participantUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var conversationIds = conversations.Select(c => c.Id).ToList();
        var unreadCounts = await _db.ConversationMessages
            .AsNoTracking()
            .Where(m => conversationIds.Contains(m.ConversationId) && m.SenderId != userId && m.ReadAt == null)
            .GroupBy(m => m.ConversationId)
            .Select(g => new { ConversationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ConversationId, x => x.Count);

        return conversations.Select(c => MapConversationResponse(c, userId, users, unreadCounts)).ToList();
    }

    public async Task<PaginatedResponse<ConversationMessageResponse>> GetMessagesAsync(Guid userId, Guid conversationId, int page, int pageSize)
    {
        var conversation = await GetAuthorizedConversationAsync(userId, conversationId);
        var currentUser = await GetMessagingUserAsync(userId);

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 30 : pageSize > 100 ? 100 : pageSize;

        var query = _db.ConversationMessages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Where(m => m.ConversationId == conversation.Id);

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
        var conversation = await GetAuthorizedConversationAsync(userId, conversationId);
        var body = request.Body?.Trim();

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new InvalidOperationException("Mesaj içeriği boş olamaz.");
        }

        if (body.Length > MaxMessageBodyLength)
        {
            throw new InvalidOperationException($"Mesaj içeriği en fazla {MaxMessageBodyLength} karakter olabilir.");
        }

        var now = DateTime.UtcNow;
        var recipientUserId = await GetOtherUserIdAsync(conversation, userId);
        var jobTitle = await _db.JobListings
            .Where(j => j.Id == conversation.JobListingId)
            .Select(j => j.Title)
            .FirstOrDefaultAsync() ?? "İlan";

        var message = new ConversationMessage
        {
            ConversationId = conversation.Id,
            SenderId = userId,
            Body = body,
            CreatedAt = now
        };

        conversation.LastMessageAt = now;
        conversation.LastMessagePreview = body.Length <= 300 ? body : body[..300];
        conversation.UpdatedAt = now;

        _db.ConversationMessages.Add(message);
        _db.Notifications.Add(new Notification
        {
            UserId = recipientUserId,
            Type = "NEW_MESSAGE",
            Title = "Yeni Mesaj",
            Body = $"{jobTitle} için yeni bir mesajınız var.",
            Data = System.Text.Json.JsonSerializer.Serialize(new { conversationId = conversation.Id })
        });

        await _db.SaveChangesAsync();

        message.Sender = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
        return MapMessageResponse(message, currentUser);
    }

    public async Task MarkConversationReadAsync(Guid userId, Guid conversationId)
    {
        await GetAuthorizedConversationAsync(userId, conversationId);

        var unreadMessages = await _db.ConversationMessages
            .Where(m => m.ConversationId == conversationId && m.SenderId != userId && m.ReadAt == null)
            .ToListAsync();

        if (unreadMessages.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        unreadMessages.ForEach(m => m.ReadAt = now);
        await _db.SaveChangesAsync();
    }

    private async Task<ConversationResponse> BuildConversationResponseAsync(Guid conversationId, Guid currentUserId)
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

        var unreadCount = await _db.ConversationMessages
            .AsNoTracking()
            .CountAsync(m => m.ConversationId == conversation.Id && m.SenderId != currentUserId && m.ReadAt == null);

        return MapConversationResponse(
            conversation,
            currentUserId,
            users,
            new Dictionary<Guid, int> { [conversation.Id] = unreadCount });
    }

    private async Task<Conversation> GetAuthorizedConversationAsync(Guid userId, Guid conversationId)
    {
        var currentUser = await GetMessagingUserAsync(userId);

        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId)
            ?? throw new KeyNotFoundException("Konuşma bulunamadı.");

        var isParticipant =
            (currentUser.AgentProfileId.HasValue && conversation.AgentId == currentUser.AgentProfileId.Value) ||
            (currentUser.SubcontractorProfileId.HasValue && conversation.SubcontractorId == currentUser.SubcontractorProfileId.Value);

        if (!isParticipant)
        {
            throw new UnauthorizedAccessException("Bu konuşmaya erişim yetkiniz yok.");
        }

        return conversation;
    }

    private async Task<MessagingUser> GetMessagingUserAsync(Guid userId)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive)
            ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");

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
            throw new UnauthorizedAccessException("Acente profili bulunamadı.");
        }

        if (user.Role == SubcontractorRole && !subcontractorProfileId.HasValue)
        {
            throw new UnauthorizedAccessException("Taşeron profili bulunamadı.");
        }

        if (user.Role != AgentRole && user.Role != SubcontractorRole)
        {
            throw new UnauthorizedAccessException("Bu hesap mesajlaşma için yetkili değil.");
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
            throw new InvalidOperationException("Bu sürümde yalnızca acente ve taşeron arasında mesajlaşma desteklenir.");
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
            throw new UnauthorizedAccessException("Bu ilan için konuşma başlatma yetkiniz yok.");
        }

        // Thursday-release rule: only allow job-linked messaging once a concrete business relation exists.
        var hasBusinessRelation = await _db.Offers.AnyAsync(o =>
                                     o.JobId == job.Id && o.SubcontractorId == participantIds.SubcontractorProfileId) ||
                                 await _db.AssignedJobs.AnyAsync(a =>
                                     a.JobId == job.Id && a.SubcontractorId == participantIds.SubcontractorProfileId);

        if (!hasBusinessRelation)
        {
            throw new InvalidOperationException("Konuşma başlatmak için ilanla ilişkili teklif veya aktif iş kaydı bulunmalıdır.");
        }

        if (currentUser.Role == AgentRole && currentUser.AgentProfileId != participantIds.AgentProfileId)
        {
            throw new UnauthorizedAccessException("Bu ilan için konuşma başlatma yetkiniz yok.");
        }

        if (currentUser.Role == SubcontractorRole)
        {
            if (currentUser.SubcontractorProfileId != participantIds.SubcontractorProfileId ||
                otherUser.AgentProfileId != participantIds.AgentProfileId)
            {
                throw new UnauthorizedAccessException("Bu ilan için konuşma başlatma yetkiniz yok.");
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
        IReadOnlyDictionary<Guid, int> unreadCounts)
    {
        var otherUserId = conversation.Agent.UserId == currentUserId
            ? conversation.Subcontractor.UserId
            : conversation.Agent.UserId;

        if (!users.TryGetValue(otherUserId, out var otherUser))
        {
            throw new KeyNotFoundException("Karşı kullanıcı bulunamadı.");
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
            LastMessagePreview = conversation.LastMessagePreview,
            LastMessageAt = conversation.LastMessageAt,
            UnreadCount = unreadCounts.TryGetValue(conversation.Id, out var unreadCount) ? unreadCount : 0,
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
            Body = message.Body,
            CreatedAt = message.CreatedAt,
            ReadAt = message.ReadAt,
            IsOwnMessage = message.SenderId == currentUser.UserId
        };
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
