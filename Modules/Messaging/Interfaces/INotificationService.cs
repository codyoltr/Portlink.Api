using Portlink.Api.DTOs.Notifications;

namespace Portlink.Api.Modules.Messaging.Interfaces;

public interface INotificationService
{
    Task<List<NotificationResponse>> GetNotificationsAsync(Guid userId, int page, int pageSize);
    Task MarkReadAsync(Guid userId, Guid notificationId);
    Task MarkAllReadAsync(Guid userId);
}
