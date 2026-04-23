using System.ComponentModel.DataAnnotations;

namespace Portlink.Api.Entities;

public class JobReport
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AssignedJobId { get; set; }
    public AssignedJob AssignedJob { get; set; } = null!;

    public Guid UploadedBy { get; set; }
    public User Uploader { get; set; } = null!;

    [Required, MaxLength(300)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string FileUrl { get; set; } = string.Empty;

    public long? FileSize { get; set; }

    [MaxLength(20)]
    public string? FileType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
