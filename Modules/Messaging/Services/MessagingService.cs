using Microsoft.EntityFrameworkCore;
using Portlink.Api.Data;
using Portlink.Api.DTOs.Notifications;
using Portlink.Api.Entities;
using Portlink.Api.Modules.Messaging.Dtos;

namespace Portlink.Api.Modules.Messaging;

public class MessageService
{
    private readonly AppDbContext _db;

    public MessageService(AppDbContext db) => _db = db;

    public async Task<List<MessageResponse>> GetMessagesAsync(Guid userId, Guid assignedJobId)
    {
        // Kullanıcının bu işe erişimi olmalı
        var hasAccess = await _db.AssignedJobs.AnyAsync(a => a.Id == assignedJobId && (a.AgentId == GetAgentId(userId) || a.SubcontractorId == GetSubId(userId)));
        // Basit erişim: gönderen veya alıcı olarak geçiyorsa göster
        var messages = await _db.Messages
            .Include(m => m.Sender)
            .Include(m => m.Receiver)
            .Where(m => m.AssignedJobId == assignedJobId && (m.SenderId == userId || m.ReceiverId == userId))
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        return messages.Select(m => new MessageResponse
        {
            Id = m.Id,
            AssignedJobId = m.AssignedJobId,
            SenderId = m.SenderId,
            SenderName = m.Sender.Email,   // Gerçek isim için profile join gerekir
            ReceiverId = m.ReceiverId,
            ReceiverName = m.Receiver.Email,
            Content = m.Content,
            IsRead = m.IsRead,
            CreatedAt = m.CreatedAt
        }).ToList();
    }

    public async Task<MessageResponse> SendMessageAsync(Guid senderId, Guid assignedJobId, Guid receiverId, string content)
    {
        var assigned = await _db.AssignedJobs.Include(a => a.JobListing)
            .FirstOrDefaultAsync(a => a.Id == assignedJobId)
            ?? throw new KeyNotFoundException("Atanmış iş bulunamadı.");

        var message = new Message
        {
            AssignedJobId = assignedJobId,
            SenderId = senderId,
            ReceiverId = receiverId,
            Content = content.Trim(),
            IsRead = false
        };
        _db.Messages.Add(message);

        // Bildirim
        _db.Notifications.Add(new Notification
        {
            UserId = receiverId,
            Type = "NEW_MESSAGE",
            Title = "Yeni Mesaj",
            Body = $"Yeni bir mesajınız var: {assigned.JobListing.Title}",
            Data = System.Text.Json.JsonSerializer.Serialize(new { assignedJobId, messageId = message.Id })
        });

        await _db.SaveChangesAsync();

        await _db.Entry(message).Reference(m => m.Sender).LoadAsync();
        await _db.Entry(message).Reference(m => m.Receiver).LoadAsync();

        return new MessageResponse
        {
            Id = message.Id,
            AssignedJobId = message.AssignedJobId,
            SenderId = message.SenderId,
            SenderName = message.Sender.Email,
            ReceiverId = message.ReceiverId,
            ReceiverName = message.Receiver.Email,
            Content = message.Content,
            IsRead = message.IsRead,
            CreatedAt = message.CreatedAt
        };
    }

    public async Task MarkReadAsync(Guid userId, Guid messageId)
    {
        var message = await _db.Messages.FirstOrDefaultAsync(m => m.Id == messageId && m.ReceiverId == userId)
            ?? throw new KeyNotFoundException("Mesaj bulunamadı.");
        message.IsRead = true;
        await _db.SaveChangesAsync();
    }

    // Helper - returns Guid.Empty if not found (simple approach)
    private static Guid GetAgentId(Guid userId) => Guid.Empty;
    private static Guid GetSubId(Guid userId) => Guid.Empty;
}

public class NotificationService
{
    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db) => _db = db;

    public async Task<List<NotificationResponse>> GetNotificationsAsync(Guid userId, int page, int pageSize)
    {
        var list = await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return list.Select(n => new NotificationResponse
        {
            Id = n.Id,
            Type = n.Type,
            Title = n.Title,
            Body = n.Body,
            IsRead = n.IsRead,
            Data = n.Data,
            CreatedAt = n.CreatedAt
        }).ToList();
    }

    public async Task MarkReadAsync(Guid userId, Guid notificationId)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId)
            ?? throw new KeyNotFoundException("Bildirim bulunamadı.");
        n.IsRead = true;
        await _db.SaveChangesAsync();
    }

    public async Task MarkAllReadAsync(Guid userId)
    {
        var notifications = await _db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
        notifications.ForEach(n => n.IsRead = true);
        await _db.SaveChangesAsync();
    }
}
