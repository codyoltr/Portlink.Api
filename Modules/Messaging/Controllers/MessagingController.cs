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
    private readonly IMessagingService _msgSvc;
    private readonly INotificationService _notifSvc;

    public MessagingController(IMessagingService msgSvc, INotificationService notifSvc)
    {
        _msgSvc = msgSvc;
        _notifSvc = notifSvc;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ─── MESSAGES ────────────────────────────────────────────────────────────

    // GET /api/messages/:assignedJobId
    [HttpGet("api/messages/{assignedJobId:guid}")]
    public async Task<IActionResult> GetMessages(Guid assignedJobId)
    {
        var result = await _msgSvc.GetMessagesAsync(UserId, assignedJobId);
        return Ok(ApiResponse<List<MessageResponse>>.Ok(result));
    }

    // POST /api/messages/:assignedJobId
    [HttpPost("api/messages/{assignedJobId:guid}")]
    public async Task<IActionResult> SendMessage(Guid assignedJobId, [FromBody] SendMessageRequest req, [FromQuery] Guid receiverId)
    {
        try
        {
            var result = await _msgSvc.SendMessageAsync(UserId, assignedJobId, receiverId, req.Content);
            return StatusCode(201, ApiResponse<MessageResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
    }

    // PUT /api/messages/:id/read
    [HttpPut("api/messages/{id:guid}/read")]
    public async Task<IActionResult> MarkMessageRead(Guid id)
    {
        try
        {
            await _msgSvc.MarkReadAsync(UserId, id);
            return Ok(ApiResponse.Ok("Okundu işaretlendi."));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
    }

    // ─── NOTIFICATIONS ───────────────────────────────────────────────────────

    // GET /api/notifications
    [HttpGet("api/notifications")]
    public async Task<IActionResult> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 30)
    {
        var result = await _notifSvc.GetNotificationsAsync(UserId, page, pageSize);
        return Ok(ApiResponse<List<NotificationResponse>>.Ok(result));
    }

    // PUT /api/notifications/:id/read
    [HttpPut("api/notifications/{id:guid}/read")]
    public async Task<IActionResult> MarkNotificationRead(Guid id)
    {
        try
        {
            await _notifSvc.MarkReadAsync(UserId, id);
            return Ok(ApiResponse.Ok("Okundu."));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
    }

    // PUT /api/notifications/read-all
    [HttpPut("api/notifications/read-all")]
    public async Task<IActionResult> MarkAllNotificationsRead()
    {
        await _notifSvc.MarkAllReadAsync(UserId);
        return Ok(ApiResponse.Ok("Tüm bildirimler okundu olarak işaretlendi."));
    }
}
