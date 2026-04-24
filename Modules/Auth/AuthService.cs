using Microsoft.EntityFrameworkCore;
using Portlink.Api.Data;
using Portlink.Api.DTOs.Auth;
using Portlink.Api.Entities;
using Portlink.Api.Helpers;

namespace Portlink.Api.Modules.Auth;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly JwtHelper _jwt;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext db, JwtHelper jwt, IConfiguration config)
    {
        _db = db;
        _jwt = jwt;
        _config = config;
    }

    // ─────────────────────────── REGISTER AGENT ─────────────────────────────

    public async Task<AuthResponse> RegisterAgentAsync(RegisterAgentRequest req)
    {
        if (await _db.Users.AnyAsync(u => u.Email == req.Email.ToLower()))
            throw new InvalidOperationException("Bu e-posta adresi zaten kayıtlı.");

        var user = new User
        {
            Email = req.Email.ToLower().Trim(),
            PasswordHash = PasswordHelper.Hash(req.Password),
            Role = "agent"
        };
        _db.Users.Add(user);

        var profile = new AgentProfile
        {
            UserId = user.Id,
            FullName = req.FullName.Trim(),
            CompanyName = req.CompanyName.Trim(),
            Phone = req.Phone?.Trim(),
            Country = req.Country?.Trim(),
            City = req.City?.Trim(),
            TaxNumber = req.TaxNumber?.Trim()
        };
        _db.AgentProfiles.Add(profile);

        await _db.SaveChangesAsync();

        return await BuildAuthResponseAsync(user, profile, null);
    }

    // ───────────────────────── REGISTER SUBCONTRACTOR ───────────────────────

    public async Task<AuthResponse> RegisterSubcontractorAsync(RegisterSubcontractorRequest req)
    {
        if (await _db.Users.AnyAsync(u => u.Email == req.Email.ToLower()))
            throw new InvalidOperationException("Bu e-posta adresi zaten kayıtlı.");

        var user = new User
        {
            Email = req.Email.ToLower().Trim(),
            PasswordHash = PasswordHelper.Hash(req.Password),
            Role = "subcontractor"
        };
        _db.Users.Add(user);

        var profile = new SubcontractorProfile
        {
            UserId = user.Id,
            FullName = req.FullName.Trim(),
            CompanyName = req.CompanyName.Trim(),
            Phone = req.Phone?.Trim(),
            Country = req.Country?.Trim(),
            City = req.City?.Trim(),
            TaxNumber = req.TaxNumber?.Trim(),
            ExpertiseTags = req.ExpertiseTags ?? new List<string>()
        };
        _db.SubcontractorProfiles.Add(profile);

        await _db.SaveChangesAsync();

        return await BuildAuthResponseAsync(user, null, profile);
    }

    // ─────────────────────────────── LOGIN ──────────────────────────────────

    public async Task<AuthResponse> LoginAsync(LoginRequest req)
    {
        var user = await _db.Users
            .Include(u => u.AgentProfile)
            .Include(u => u.SubcontractorProfile)
            .FirstOrDefaultAsync(u => u.Email == req.Email.ToLower())
            ?? throw new UnauthorizedAccessException("E-posta veya şifre hatalı.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Hesabınız devre dışı bırakılmış.");

        if (!PasswordHelper.Verify(req.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("E-posta veya şifre hatalı.");

        return await BuildAuthResponseAsync(user, user.AgentProfile, user.SubcontractorProfile);
    }

    // ──────────────────────────── REFRESH TOKEN ─────────────────────────────

    public async Task<AuthResponse> RefreshAsync(string rawRefreshToken)
    {
        var hash = _jwt.HashToken(rawRefreshToken);

        var stored = await _db.RefreshTokens
            .Include(r => r.User)
                .ThenInclude(u => u.AgentProfile)
            .Include(r => r.User)
                .ThenInclude(u => u.SubcontractorProfile)
            .FirstOrDefaultAsync(r => r.TokenHash == hash && r.ExpiresAt > DateTime.UtcNow)
            ?? throw new UnauthorizedAccessException("Geçersiz veya süresi dolmuş token.");

        // Eski token'ı sil (token rotation)
        _db.RefreshTokens.Remove(stored);
        await _db.SaveChangesAsync();

        return await BuildAuthResponseAsync(stored.User, stored.User.AgentProfile, stored.User.SubcontractorProfile);
    }

    // ──────────────────────────────── LOGOUT ────────────────────────────────

    public async Task LogoutAsync(Guid userId)
    {
        var tokens = _db.RefreshTokens.Where(r => r.UserId == userId);
        _db.RefreshTokens.RemoveRange(tokens);
        await _db.SaveChangesAsync();
    }

    // ────────────────────────────── GET ME ──────────────────────────────────

    public async Task<UserProfileResponse> GetMeAsync(Guid userId)
    {
        var user = await _db.Users
            .Include(u => u.AgentProfile)
            .Include(u => u.SubcontractorProfile)
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");

        return MapUserProfile(user, user.AgentProfile, user.SubcontractorProfile);
    }

    // ─────────────────────────────── PRIVATE ────────────────────────────────

    private async Task<AuthResponse> BuildAuthResponseAsync(
        User user,
        AgentProfile? agent,
        SubcontractorProfile? sub)
    {
        var accessToken = _jwt.GenerateAccessToken(user.Id, user.Email, user.Role);
        var rawRefresh = _jwt.GenerateRefreshToken();
        var hash = _jwt.HashToken(rawRefresh);

        var days = int.Parse(_config["Jwt:RefreshTokenDays"] ?? "7");

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddDays(days)
        };
        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefresh,
            User = MapUserProfile(user, agent, sub)
        };
    }

    private static UserProfileResponse MapUserProfile(User user, AgentProfile? agent, SubcontractorProfile? sub)
    {
        object? profile = null;

        if (agent != null)
        {
            profile = new AgentProfileResponse
            {
                Id = agent.Id,
                FullName = agent.FullName,
                CompanyName = agent.CompanyName,
                Phone = agent.Phone,
                Country = agent.Country,
                City = agent.City,
                LogoUrl = agent.LogoUrl,
                Rating = agent.Rating,
                TotalJobs = agent.TotalJobs,
                IsVerified = agent.IsVerified
            };
        }
        else if (sub != null)
        {
            profile = new SubcontractorProfileResponse
            {
                Id = sub.Id,
                FullName = sub.FullName,
                CompanyName = sub.CompanyName,
                Phone = sub.Phone,
                Country = sub.Country,
                City = sub.City,
                LogoUrl = sub.LogoUrl,
                Rating = sub.Rating,
                TotalCompleted = sub.TotalCompleted,
                ExpertiseTags = sub.ExpertiseTags?.ToList() ?? new(),
                IsVerified = sub.IsVerified
            };
        }

        return new UserProfileResponse
        {
            Id = user.Id,
            Email = user.Email,
            Role = user.Role,
            IsVerified = user.IsVerified,
            Profile = profile
        };
    }
}
