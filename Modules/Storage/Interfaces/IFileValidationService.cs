using Portlink.Api.Modules.Storage.Enums;
namespace Portlink.Api.Modules.Storage.Interfaces;

public interface IFileValidationService
{
    Task<ValidatedStorageFile> ValidateAsync(IFormFile file, StorageFileCategory expectedCategory, CancellationToken cancellationToken = default);
    void EnsureCategoryMatchesExistingFile(StorageFileCategory category, string fileExtension, string mimeType);
}

public sealed class ValidatedStorageFile
{
    public required string OriginalFileName { get; init; }
    public required string SafeFileName { get; init; }
    public required string FileExtension { get; init; }
    public required string MimeType { get; init; }
    public required long SizeInBytes { get; init; }
    public required StorageFileCategory FileCategory { get; init; }
    public required MemoryStream Content { get; init; }
}
