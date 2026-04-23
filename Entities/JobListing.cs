using System.ComponentModel.DataAnnotations;

namespace Portlink.Api.Entities;

public class JobListing
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AgentId { get; set; }
    public AgentProfile Agent { get; set; } = null!;

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    /// <summary>'subcontractor' | 'agency-partnership'</summary>
    [Required, MaxLength(30)]
    public string ListingType { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ShipName { get; set; }

    [MaxLength(20)]
    public string? PortCode { get; set; }

    [MaxLength(300)]
    public string? PortName { get; set; }

    [MaxLength(300)]
    public string? Location { get; set; }

    [Required, MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    /// <summary>Seçilen alt hizmetler — PostgreSQL text[]</summary>
    public List<string> SelectedServices { get; set; } = new List<string>();

    public DateTime? Eta { get; set; }
    public string? NeedText { get; set; }

    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "TRY";

    /// <summary>'active' | 'reviewing' | 'completed' | 'cancelled'</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "active";

    /// <summary>Denormalize sayaç (trigger / uygulama katmanında güncellenir)</summary>
    public int OfferCount { get; set; } = 0;

    public DateTime? Deadline { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<JobFile> JobFiles { get; set; } = new List<JobFile>();
    public ICollection<Offer> Offers { get; set; } = new List<Offer>();
    public AssignedJob? AssignedJob { get; set; }
}
