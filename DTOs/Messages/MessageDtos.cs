namespace Portlink.Api.DTOs.Messages;

// ──────────────────── REQUEST ────────────────────

public class SendMessageRequest
{
    public string Content { get; set; } = string.Empty;
}

// ──────────────────── RESPONSE ───────────────────

public class MessageResponse
{
    public Guid Id { get; set; }
    public Guid? AssignedJobId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public Guid ReceiverId { get; set; }
    public string ReceiverName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
