namespace Portlink.Api.Modules.Storage.Interfaces;

public interface IS3StorageProvider
{
    Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<StorageProviderDownloadResult> DownloadAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
    string GeneratePresignedDownloadUrl(string key, string downloadFileName, TimeSpan expiresIn);
    string GeneratePresignedViewUrl(string key, TimeSpan expiresIn);
}

public sealed class StorageProviderDownloadResult
{
    public required Stream Stream { get; init; }
    public required string ContentType { get; init; }
    public long? ContentLength { get; init; }
}
