using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portlink.Api.DTOs.Auth;
using Portlink.Api.DTOs.Common;
using System.Security.Claims;

namespace Portlink.Api.Modules.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    // POST /api/auth/register/agent
    [HttpPost("register/agent")]
    public async Task<IActionResult> RegisterAgent([FromBody] RegisterAgentRequest req)
    {
        try
        {
            var result = await _authService.RegisterAgentAsync(req);
            return StatusCode(201, ApiResponse<AuthResponse>.Ok(result, "Kayıt başarılı."));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse.Fail(ex.Message));
        }
    }

    // POST /api/auth/register/subcontractor
    [HttpPost("register/subcontractor")]
    public async Task<IActionResult> RegisterSubcontractor([FromBody] RegisterSubcontractorRequest req)
    {
        try
        {
            var result = await _authService.RegisterSubcontractorAsync(req);
            return StatusCode(201, ApiResponse<AuthResponse>.Ok(result, "Kayıt başarılı."));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse.Fail(ex.Message));
        }
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        try
        {
            var result = await _authService.LoginAsync(req);
            return Ok(ApiResponse<AuthResponse>.Ok(result));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse.Fail(ex.Message));
        }
    }

    // POST /api/auth/refresh
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest req)
    {
        try
        {
            var result = await _authService.RefreshAsync(req.RefreshToken);
            return Ok(ApiResponse<AuthResponse>.Ok(result));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse.Fail(ex.Message));
        }
    }

    // POST /api/auth/logout
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _authService.LogoutAsync(userId);
        return Ok(ApiResponse.Ok("Çıkış yapıldı."));
    }

    // GET /api/auth/me
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        try
        {
            var result = await _authService.GetMeAsync(userId);
            return Ok(ApiResponse<UserProfileResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }
}
