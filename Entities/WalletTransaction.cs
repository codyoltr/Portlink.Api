using System.ComponentModel.DataAnnotations;

namespace Portlink.Api.Entities;

public class WalletTransaction
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SubcontractorId { get; set; }
    public SubcontractorProfile Subcontractor { get; set; } = null!;

    public Guid? AssignedJobId { get; set; }
    public AssignedJob? AssignedJob { get; set; }

    /// <summary>'earning' | 'pending' | 'withdrawal'</summary>
    [MaxLength(30)]
    public string Type { get; set; } = string.Empty;

    [Required]
    public decimal Amount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "TRY";

    /// <summary>'pending' | 'completed'</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "pending";

    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
