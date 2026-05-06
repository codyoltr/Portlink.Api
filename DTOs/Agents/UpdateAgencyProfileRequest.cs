namespace Portlink.Api.DTOs.Agents;
//şimdilik bunlar yeter.
public class UpdateAgencyProfileRequest
{
    public string? CompanyName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Bio { get; set; }
    public List<Guid>? PortIds { get; set; }
}
