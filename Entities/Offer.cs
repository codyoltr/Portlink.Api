using System.ComponentModel.DataAnnotations;

namespace Portlink.Api.Entities;

public class Offer
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid JobId { get; set; }
    public JobListing JobListing { get; set; } = null!;

    public Guid SubcontractorId { get; set; }
    public SubcontractorProfile Subcontractor { get; set; } = null!;

    [Required]
    public decimal Price { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "TRY";

    /// <summary>Kaç günde tamamlanır</summary>
    public int? EstimatedDays { get; set; }

    public string? CoverNote { get; set; }

    /// <summary>'pending' | 'accepted' | 'rejected' | 'withdrawn'</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public AssignedJob? AssignedJob { get; set; }
}
