using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portlink.Api.Modules.Auth.Dtos;
using Portlink.Api.Modules.Common.Dtos;
using System.Security.Claims;

namespace Portlink.Api.Modules.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    // POST /api/auth/register/agent
    [HttpPost("register/agent")]
    public async Task<IActionResult> RegisterAgent([FromBody] RegisterAgentRequest req)
    {
        var result = await _authService.RegisterAgentAsync(req);
        return StatusCode(201, ApiResponse<AuthResponse>.Ok(result, "Kayıt başarılı."));
    }

    // POST /api/auth/register/subcontractor
    [HttpPost("register/subcontractor")]
    public async Task<IActionResult> RegisterSubcontractor([FromBody] RegisterSubcontractorRequest req)
    {
        var result = await _authService.RegisterSubcontractorAsync(req);
        return StatusCode(201, ApiResponse<AuthResponse>.Ok(result, "Kayıt başarılı."));
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var result = await _authService.LoginAsync(req);
        return Ok(ApiResponse<AuthResponse>.Ok(result));
    }

    // POST /api/auth/refresh
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest req)
    {
        var result = await _authService.RefreshAsync(req.RefreshToken);
        return Ok(ApiResponse<AuthResponse>.Ok(result));
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
        var result = await _authService.GetMeAsync(userId);
        return Ok(ApiResponse<UserProfileResponse>.Ok(result));
    }
}
