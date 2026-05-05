using Microsoft.EntityFrameworkCore;
using Portlink.Api.Data;
using Portlink.Api.DTOs.Notifications;
using Portlink.Api.Modules.Messaging.Interfaces;

namespace Portlink.Api.Modules.Messaging;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<NotificationResponse>> GetNotificationsAsync(Guid userId, int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 30 : pageSize > 100 ? 100 : pageSize;

        var list = await _db.Notifications
            .AsNoTracking()
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

    public async Task MarkNotificationReadAsync(Guid userId, Guid notificationId)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId)
            ?? throw new KeyNotFoundException("Bildirim bulunamadı.");

        notification.IsRead = true;
        await _db.SaveChangesAsync();
    }

    public async Task MarkAllNotificationReadAsync(Guid userId)
    {
        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        notifications.ForEach(n => n.IsRead = true);
        await _db.SaveChangesAsync();
    }
}
