using System.ComponentModel.DataAnnotations;

namespace Portlink.Api.Entities;

public class TeamMemberData
{
    public string Title { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Icon { get; set; } = string.Empty;
}

public class CompanyReferenceData
{
    public string Name { get; set; } = string.Empty;
    public string Year { get; set; } = string.Empty;
}

public class SubcontractorProfile
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

    [MaxLength(100)]
    public string? CompanyType { get; set; }

    [MaxLength(10)]
    public string? FoundedYear { get; set; }

    [MaxLength(100)]
    public string? Experience { get; set; }

    [MaxLength(1000)]
    public string? Bio { get; set; }

    public List<string> ServiceRegions { get; set; } = new();
    public List<TeamMemberData> TeamStructure { get; set; } = new();
    public List<CompanyReferenceData> CompanyReferences { get; set; } = new();

    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    [MaxLength(500)]
    public string? LogoS3Key { get; set; }

    public decimal Rating { get; set; } = 0.0m;
    public int RatingCount { get; set; } = 0;
    public int TotalCompleted { get; set; } = 0;
    public bool IsVerified { get; set; } = false;

    /// <summary>['Makine Bakımı', 'Elektrik', ...] — PostgreSQL text[]</summary>
    public List<string> ExpertiseTags { get; set; } = new List<string>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Offer> Offers { get; set; } = new List<Offer>();
    public ICollection<AssignedJob> AssignedJobs { get; set; } = new List<AssignedJob>();
    public ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
}
