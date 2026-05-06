using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portlink.Api.DTOs.Notifications;
using Portlink.Api.Modules.Common.Dtos;
using Portlink.Api.Modules.Messaging.Dtos;
using Portlink.Api.Modules.Messaging.Interfaces;
using System.Security.Claims;

namespace Portlink.Api.Modules.Messaging;

[ApiController]
[Authorize]
public class MessagingController : ControllerBase
{
    private readonly IMessagingService _messagingService;
    private readonly INotificationService _notificationService;

    public MessagingController(IMessagingService messagingService, INotificationService notificationService)
    {
        _messagingService = messagingService;
        _notificationService = notificationService;
    }

    private Guid UserId
    {
        get
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);

            if (claim == null || !Guid.TryParse(claim, out var userId))
            {
                throw new UnauthorizedAccessException("Kimlik bilgisi alınamadı.");
            }

            return userId;
        }
    }

    [HttpPost("api/conversations/start")]
    public async Task<IActionResult> StartConversation([FromBody] StartConversationRequest request)
    {
        var result = await _messagingService.StartConversationAsync(UserId, request);
        return Ok(ApiResponse<ConversationResponse>.Ok(result));
    }

    [HttpGet("api/conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var result = await _messagingService.GetConversationsAsync(UserId);
        return Ok(ApiResponse<List<ConversationResponse>>.Ok(result));
    }

    [HttpGet("api/conversations/{conversationId:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid conversationId, [FromQuery] int page = 1, [FromQuery] int pageSize = 30)
    {
        var result = await _messagingService.GetMessagesAsync(UserId, conversationId, page, pageSize);
        return Ok(ApiResponse<PaginatedResponse<ConversationMessageResponse>>.Ok(result));
    }

    [HttpPost("api/conversations/{conversationId:guid}/messages")]
    public async Task<IActionResult> SendMessage(Guid conversationId, [FromBody] SendConversationMessageRequest request)
    {
        var result = await _messagingService.SendMessageAsync(UserId, conversationId, request);
        return StatusCode(201, ApiResponse<ConversationMessageResponse>.Ok(result));
    }

    [HttpPatch("api/conversations/{conversationId:guid}/messages/{messageId:guid}")]
    public async Task<IActionResult> EditMessage(Guid conversationId, Guid messageId, [FromBody] EditConversationMessageRequest request)
    {
        var result = await _messagingService.EditMessageAsync(UserId, conversationId, messageId, request);
        return Ok(ApiResponse<ConversationMessageResponse>.Ok(result));
    }

    [HttpDelete("api/conversations/{conversationId:guid}/messages/{messageId:guid}/for-me")]
    public async Task<IActionResult> DeleteMessageForMe(Guid conversationId, Guid messageId)
    {
        await _messagingService.DeleteMessageForMeAsync(UserId, conversationId, messageId);
        return Ok(ApiResponse.Ok("Mesaj sizin iÃ§in gizlendi."));
    }

    [HttpDelete("api/conversations/{conversationId:guid}/messages/{messageId:guid}/for-everyone")]
    public async Task<IActionResult> DeleteMessageForEveryone(Guid conversationId, Guid messageId)
    {
        await _messagingService.DeleteMessageForEveryoneAsync(UserId, conversationId, messageId);
        return Ok(ApiResponse.Ok("Mesaj tÃ¼m katÄ±lÄ±mcÄ±lar iÃ§in silindi."));
    }

    [HttpPost("api/conversations/{conversationId:guid}/clear-history")]
    public async Task<IActionResult> ClearConversationHistory(Guid conversationId)
    {
        await _messagingService.ClearConversationHistoryAsync(UserId, conversationId);
        return Ok(ApiResponse.Ok("KonuÅŸma geÃ§miÅŸi sizin iÃ§in temizlendi."));
    }

    [HttpPost("api/conversations/{conversationId:guid}/read")]
    public async Task<IActionResult> MarkConversationRead(Guid conversationId)
    {
        await _messagingService.MarkConversationReadAsync(UserId, conversationId);
        return Ok(ApiResponse.Ok("Konuşma okundu olarak işaretlendi."));
    }

    [HttpGet("api/notifications")]
    public async Task<IActionResult> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 30)
    {
        var result = await _notificationService.GetNotificationsAsync(UserId, page, pageSize);
        return Ok(ApiResponse<List<NotificationResponse>>.Ok(result));
    }

    [HttpPut("api/notifications/{id:guid}/read")]
    public async Task<IActionResult> MarkNotificationRead(Guid id)
    {
        await _notificationService.MarkNotificationReadAsync(UserId, id);
        return Ok(ApiResponse.Ok("Bildirim okundu olarak işaretlendi."));
    }

    [HttpPut("api/notifications/read-all")]
    public async Task<IActionResult> MarkAllNotificationsRead()
    {
        await _notificationService.MarkAllNotificationReadAsync(UserId);
        return Ok(ApiResponse.Ok("Tüm bildirimler okundu olarak işaretlendi."));
    }
}
