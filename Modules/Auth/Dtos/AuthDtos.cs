namespace Portlink.Api.Modules.Auth.Dtos;
// ──────────────────── REQUEST ────────────────────

public class RegisterAgentRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? TaxNumber { get; set; }
}

public class RegisterSubcontractorRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? TaxNumber { get; set; }
    public List<string>? ExpertiseTags { get; set; }
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

// ──────────────────── RESPONSE ───────────────────

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public UserProfileResponse User { get; set; } = null!;
}

public class UserProfileResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public object? Profile { get; set; }  // AgentProfileResponse | SubcontractorProfileResponse
}

public class AgentProfileResponse
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? LogoUrl { get; set; }
    public decimal Rating { get; set; }
    public int TotalJobs { get; set; }
    public bool IsVerified { get; set; }
}

public class SubcontractorProfileResponse
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? LogoUrl { get; set; }
    public decimal Rating { get; set; }
    public int TotalCompleted { get; set; }
    public List<string> ExpertiseTags { get; set; } = new();
    public bool IsVerified { get; set; }
}
