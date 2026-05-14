using System.ComponentModel.DataAnnotations;

namespace Portlink.Api.Modules.Chatbot.Dtos;

public class ChatbotMessageRequest
{
    [Required]
    [MaxLength(4000)]
    public string Message { get; set; } = string.Empty;

    public List<ChatbotRecentMessageDto>? RecentMessages { get; set; }
    public ChatbotContextDto? Context { get; set; }
}

public class ChatbotRecentMessageDto
{
    [Required]
    [MaxLength(20)]
    public string Role { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;
}

public class ChatbotContextDto
{
    [MaxLength(200)]
    public string? Route { get; set; }

    [MaxLength(120)]
    public string? PageName { get; set; }

    [MaxLength(60)]
    public string? UserRole { get; set; }
}

public class ChatbotMessageResponse
{
    public string Answer { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}

public sealed class ChatbotUserContext
{
    public Guid? UserId { get; init; }
    public string? Role { get; init; }
    public bool IsAuthenticated => UserId.HasValue;
}

public sealed class LlmPromptMessage
{
    public string Role { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}

public sealed class LlmPromptRequest
{
    public string SystemPrompt { get; init; } = string.Empty;
    public IReadOnlyCollection<LlmPromptMessage> Messages { get; init; } = Array.Empty<LlmPromptMessage>();
}

public sealed class LlmProviderResponse
{
    public string Answer { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
}
