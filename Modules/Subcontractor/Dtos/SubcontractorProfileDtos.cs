using Portlink.Api.Modules.Common.Dtos;

namespace Portlink.Api.Modules.Subcontractor.Dtos;

public class SubcontractorProfileDetailResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyType { get; set; }
    public string? Phone { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? TaxNumber { get; set; }
    public string? LogoUrl { get; set; }
    public decimal Rating { get; set; }
    public int TotalCompleted { get; set; }
    public List<string> ExpertiseTags { get; set; } = new();
    public List<PortResponse> ServicePorts { get; set; } = new();
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateSubcontractorProfileRequest
{
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyType { get; set; }
    public string? Phone { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? TaxNumber { get; set; }
    public string? LogoUrl { get; set; }
    public List<string>? ExpertiseTags { get; set; }
    public List<Guid>? ServicePortIds { get; set; }
}
