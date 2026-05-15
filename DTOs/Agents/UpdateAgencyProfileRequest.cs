namespace Portlink.Api.DTOs.Agents;

public class UpdateAgencyProfileRequest
{
    public string? FullName { get; set; }
    public string? CompanyName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Bio { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public List<Guid>? PortIds { get; set; }
    public List<string>? ServiceScopes { get; set; }
}
