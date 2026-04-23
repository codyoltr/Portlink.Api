using System.ComponentModel.DataAnnotations;

namespace Portlink.Api.Entities;

public class JobFile
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid JobId { get; set; }
    public JobListing JobListing { get; set; } = null!;

    [Required, MaxLength(300)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>Byte cinsinden boyut</summary>
    public long? FileSize { get; set; }

    /// <summary>'pdf' | 'image' | 'zip'</summary>
    [MaxLength(20)]
    public string? FileType { get; set; }

    public Guid UploadedBy { get; set; }
    public User Uploader { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
