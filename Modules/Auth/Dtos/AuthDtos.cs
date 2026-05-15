using Portlink.Api.Modules.Common.Dtos;

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
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Bio { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? LogoUrl { get; set; }
    public decimal Rating { get; set; }
    public int RatingCount { get; set; }
    public int TotalJobs { get; set; }
    public List<string> ServiceScopes { get; set; } = new();
    public bool IsVerified { get; set; }
    public bool HasCurrentUserRated { get; set; }
    public Dictionary<int, int> RatingBreakdown { get; set; } = new();
    public List<PortResponse> Ports { get; set; } = new();
}

public class TeamMemberResponse
{
    public string Title { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Icon { get; set; } = string.Empty;
}

public class CompanyReferenceResponse
{
    public string Name { get; set; } = string.Empty;
    public string Year { get; set; } = string.Empty;
}

public class SubcontractorProfileResponse
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyType { get; set; }
    public string? FoundedYear { get; set; }
    public string? Experience { get; set; }
    public string? Bio { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? LogoUrl { get; set; }
    public decimal Rating { get; set; }
    public int RatingCount { get; set; }
    public int TotalCompleted { get; set; }
    public List<string> ExpertiseTags { get; set; } = new();
    public List<string> ServiceRegions { get; set; } = new();
    public List<TeamMemberResponse> TeamStructure { get; set; } = new();
    public List<CompanyReferenceResponse> CompanyReferences { get; set; } = new();
    public bool IsVerified { get; set; }
    public bool HasCurrentUserRated { get; set; }
    public Dictionary<int, int> RatingBreakdown { get; set; } = new();
}

public class UpdateSubcontractorProfileRequest
{
    public string? CompanyName { get; set; }
    public string? FullName { get; set; }
    public string? CompanyType { get; set; }
    public string? FoundedYear { get; set; }
    public string? Experience { get; set; }
    public string? Bio { get; set; }
    public string? Phone { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public List<string>? ExpertiseTags { get; set; }
    public List<string>? ServiceRegions { get; set; }
    public List<TeamMemberResponse>? TeamStructure { get; set; }
    public List<CompanyReferenceResponse>? CompanyReferences { get; set; }
}
