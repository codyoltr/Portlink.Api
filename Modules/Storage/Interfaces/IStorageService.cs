using Portlink.Api.Modules.Common.Dtos;
using Portlink.Api.Modules.Storage.Dtos;

namespace Portlink.Api.Modules.Storage.Interfaces;

public interface IStorageService
{
    Task<StorageFileResponse> UploadFileAsync(Guid userId, UploadStorageFileRequest request, CancellationToken cancellationToken = default);
    Task<PaginatedResponse<StorageFileResponse>> GetFilesAsync(Guid userId, StorageFileQueryRequest request, CancellationToken cancellationToken = default);
    Task<StorageFileResponse> GetFileByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<StorageFileStreamResult> DownloadFileAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<StorageFileStreamResult> PreviewFileAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<StorageAccessUrlResponse> GenerateAccessUrlAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<StorageFileResponse> UpdateMetadataAsync(Guid userId, Guid id, UpdateStorageFileMetadataRequest request, CancellationToken cancellationToken = default);
    Task<StorageFileResponse> ReplaceFileAsync(Guid userId, Guid id, ReplaceStorageFileRequest request, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}
