using System.ComponentModel.DataAnnotations;

namespace Portlink.Api.Entities;

public class JobWorkflowLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AssignedJobId { get; set; }
    public AssignedJob AssignedJob { get; set; } = null!;

    public Guid CreatedBy { get; set; }
    public User Creator { get; set; } = null!;

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [MaxLength(30)]
    public string Type { get; set; } = "note";

    [MaxLength(500)]
    public string? FileUrl { get; set; }

    [MaxLength(300)]
    public string? FileName { get; set; }

    [MaxLength(20)]
    public string? FileType { get; set; }

    public long? FileSize { get; set; }

    [MaxLength(30)]
    public string ReviewStatus { get; set; } = "none";

    public Guid? ReviewedBy { get; set; }
    public User? Reviewer { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
