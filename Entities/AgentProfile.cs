using System.ComponentModel.DataAnnotations;

namespace Portlink.Api.Entities;

public class AgentProfile
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    [Required, MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Phone { get; set; }

    [Required, MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Country { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(50)]
    public string? TaxNumber { get; set; }

    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    public decimal Rating { get; set; } = 0.0m;
    public int TotalJobs { get; set; } = 0;
    public bool IsVerified { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<JobListing> JobListings { get; set; } = new List<JobListing>();
    public ICollection<AssignedJob> AssignedJobs { get; set; } = new List<AssignedJob>();
}
