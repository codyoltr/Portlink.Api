using System.ComponentModel.DataAnnotations;
using Portlink.Api.Modules.Auth.Entities;

namespace Portlink.Api.Entities;

public class AssignedJob
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid JobId { get; set; }
    public JobListing JobListing { get; set; } = null!;

    public Guid OfferId { get; set; }
    public Offer Offer { get; set; } = null!;

    public Guid AgentId { get; set; }
    public AgentProfile Agent { get; set; } = null!;

    public Guid SubcontractorId { get; set; }
    public SubcontractorProfile Subcontractor { get; set; } = null!;

    /// <summary>0-100 yüzde tamamlanma</summary>
    public int Progress { get; set; } = 0;

    /// <summary>'planning' | 'in_progress' | 'review' | 'completed'</summary>
    [MaxLength(30)]
    public string Status { get; set; } = "planning";

    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<JobLog> JobLogs { get; set; } = new List<JobLog>();
    public ICollection<JobReport> JobReports { get; set; } = new List<JobReport>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
}
