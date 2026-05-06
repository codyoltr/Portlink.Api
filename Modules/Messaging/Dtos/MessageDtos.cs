using System.ComponentModel.DataAnnotations;
using Portlink.Api.Modules.Common.Dtos;

namespace Portlink.Api.Modules.Messaging.Dtos;

public class StartConversationRequest
{
    [Required]
    public Guid OtherUserId { get; set; }

    [Required]
    public Guid JobListingId { get; set; }
}

public class SendConversationMessageRequest
{
    [Required]
    [MaxLength(4000)]
    public string Body { get; set; } = string.Empty;
}

public class EditConversationMessageRequest
{
    [Required]
    [MaxLength(4000)]
    public string Body { get; set; } = string.Empty;
}

public class ConversationResponse
{
    public Guid Id { get; set; }
    public Guid? JobListingId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public Guid OtherUserId { get; set; }
    public string OtherRole { get; set; } = string.Empty;
    public string OtherCompanyName { get; set; } = string.Empty;
    public string? OtherFullName { get; set; }
    public string? LastMessagePreview { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ConversationMessageResponse
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderRole { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool IsOwnMessage { get; set; }
    public bool IsEdited { get; set; }
    public DateTime? EditedAt { get; set; }
    public bool IsDeletedForEveryone { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }
}

public class ConversationMessagesResponse : PaginatedResponse<ConversationMessageResponse>
{
}
