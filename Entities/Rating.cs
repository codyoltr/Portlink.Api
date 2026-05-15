using System.ComponentModel.DataAnnotations;

namespace Portlink.Api.Entities;

public class Rating
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RaterUserId { get; set; }
    public User RaterUser { get; set; } = null!;

    // Profile ID of the rated party (SubcontractorProfile.Id or AgentProfile.Id)
    public Guid RateeProfileId { get; set; }

    public decimal Score { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
