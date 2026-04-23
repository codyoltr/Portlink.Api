namespace Portlink.Api.DTOs.Notifications;

public class NotificationResponse
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public bool IsRead { get; set; }
    public Dictionary<string, string>? Data { get; set; }
    public DateTime CreatedAt { get; set; }
}
