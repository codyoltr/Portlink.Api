using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Portlink.Api.Modules.Storage.Entities;
using Portlink.Api.Modules.Storage.Enums;

namespace Portlink.Api.Modules.Storage.Dtos;

public class UploadStorageFileRequest
{
    [Required]
    public IFormFile? File { get; set; }

    [Required]
    public StorageFileCategory FileCategory { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public StorageRelatedEntityType RelatedEntityType { get; set; } = StorageRelatedEntityType.None;
    public Guid? RelatedEntityId { get; set; }
}

public class UpdateStorageFileMetadataRequest
{
    public StorageFileCategory? FileCategory { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public StorageRelatedEntityType? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
}

public class ReplaceStorageFileRequest
{
    [Required]
    public IFormFile? File { get; set; }
}

public class StorageFileQueryRequest
{
    public StorageFileCategory? FileCategory { get; set; }
    public Guid? UploadedByUserId { get; set; }
    public StorageRelatedEntityType? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? MimeType { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
}

public class StorageFileResponse
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public StorageFileCategory FileCategory { get; set; }
    public string? Description { get; set; }
    public StorageRelatedEntityType RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime? ReplacedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public StorageUploadedByResponse? UploadedBy { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public string PreviewUrl { get; set; } = string.Empty;
    public string AccessUrlEndpoint { get; set; } = string.Empty;
}

public class StorageUploadedByResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class StorageAccessUrlResponse
{
    public string Url { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public sealed class StorageFileStreamResult
{
    public required Stream Stream { get; init; }
    public required string MimeType { get; init; }
    public required string FileName { get; init; }
    public long? SizeInBytes { get; init; }
}
