using System.ComponentModel.DataAnnotations;
using Portlink.Api.Entities;
using Portlink.Api.Modules.Storage.Enums;

namespace Portlink.Api.Modules.Storage.Entities;

public class StorageFile
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(300)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string StoredFileName { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string SafeFileName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string FileExtension { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string MimeType { get; set; } = string.Empty;

    public long SizeInBytes { get; set; }

    [Required, MaxLength(200)]
    public string BucketName { get; set; } = string.Empty;

    [Required, MaxLength(600)]
    public string S3Key { get; set; } = string.Empty;

    public StorageFileCategory FileCategory { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public StorageRelatedEntityType RelatedEntityType { get; set; } = StorageRelatedEntityType.None;
    public Guid? RelatedEntityId { get; set; }

    public Guid UploadedByUserId { get; set; }
    public User UploadedByUser { get; set; } = null!;

    public DateTime? ReplacedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
