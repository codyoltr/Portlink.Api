using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portlink.Api.Modules.Chatbot.Dtos;
using Portlink.Api.Modules.Chatbot.Interfaces;
using Portlink.Api.Modules.Common.Dtos;

namespace Portlink.Api.Modules.Chatbot.Controllers;

[ApiController]
[Route("api/chatbot")]
[AllowAnonymous]
public class ChatbotController : ControllerBase
{
    private readonly IChatbotService _chatbotService;

    public ChatbotController(IChatbotService chatbotService)
    {
        _chatbotService = chatbotService;
    }

    [HttpPost("message")]
    public async Task<IActionResult> SendMessage([FromBody] ChatbotMessageRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _chatbotService.SendMessageAsync(BuildUserContext(), request, cancellationToken);
            return Ok(ApiResponse<ChatbotMessageResponse>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse.Fail("The assistant is currently unavailable. Please try again later."));
        }
    }

    private ChatbotUserContext BuildUserContext()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = User.FindFirstValue(ClaimTypes.Role);

        return new ChatbotUserContext
        {
            UserId = Guid.TryParse(userIdValue, out var userId) ? userId : null,
            Role = string.IsNullOrWhiteSpace(role) ? null : role
        };
    }
}
