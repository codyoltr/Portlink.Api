using Microsoft.EntityFrameworkCore;
using Portlink.Api.Data;
using Portlink.Api.Entities;
using Portlink.Api.Modules.Common.Dtos;
using Portlink.Api.Modules.Storage.Dtos;
using Portlink.Api.Modules.Storage.Entities;
using Portlink.Api.Modules.Storage.Interfaces;
using Portlink.Api.Modules.Storage.Enums;
using Portlink.Api.Modules.Storage.Settings;

namespace Portlink.Api.Modules.Storage.Services;

public class StorageService : IStorageService
{
    private const string AgentRole = "agent";
    private const string SubcontractorRole = "subcontractor";

    private readonly AppDbContext _db;
    private readonly IS3StorageProvider _storageProvider;
    private readonly IFileValidationService _fileValidationService;
    private readonly StorageSettings _settings;
    private readonly ILogger<StorageService> _logger;

    public StorageService(
        AppDbContext db,
        IS3StorageProvider storageProvider,
        IFileValidationService fileValidationService,
        StorageSettings settings,
        ILogger<StorageService> logger)
    {
        _db = db;
        _storageProvider = storageProvider;
        _fileValidationService = fileValidationService;
        _settings = settings;
        _logger = logger;
    }

    public async Task<StorageFileResponse> UploadFileAsync(Guid userId, UploadStorageFileRequest request, CancellationToken cancellationToken = default)
    {
        var currentUser = await GetCurrentUserAsync(userId, cancellationToken);
        EnsureRelatedEntityInput(request.RelatedEntityType, request.RelatedEntityId);
        await EnsureCanAttachToRelatedEntityAsync(currentUser, request.RelatedEntityType, request.RelatedEntityId, cancellationToken);

        var formFile = request.File ?? throw new InvalidOperationException("Dosya gereklidir.");
        ValidatedStorageFile validatedFile;

        try
        {
            validatedFile = await _fileValidationService.ValidateAsync(
                formFile,
                request.FileCategory,
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Dosya validasyonu basarisiz. UserId: {UserId}, FileName: {FileName}, MimeType: {MimeType}, Size: {Size}, Category: {Category}, RelatedEntityType: {RelatedEntityType}, RelatedEntityId: {RelatedEntityId}",
                userId,
                formFile.FileName,
                formFile.ContentType,
                formFile.Length,
                request.FileCategory,
                request.RelatedEntityType,
                request.RelatedEntityId);
            throw;
        }

        var storedFileName = $"{Guid.NewGuid():N}-{validatedFile.SafeFileName}.{validatedFile.FileExtension}";
        var key = BuildStorageKey(validatedFile.FileCategory, storedFileName);

        await _storageProvider.UploadAsync(key, validatedFile.Content, validatedFile.MimeType, cancellationToken);

        var entity = new StorageFile
        {
            OriginalFileName = validatedFile.OriginalFileName,
            StoredFileName = storedFileName,
            SafeFileName = validatedFile.SafeFileName,
            FileExtension = validatedFile.FileExtension,
            MimeType = validatedFile.MimeType,
            SizeInBytes = validatedFile.SizeInBytes,
            BucketName = _settings.S3BucketName,
            S3Key = key,
            FileCategory = validatedFile.FileCategory,
            Description = NormalizeDescription(request.Description),
            RelatedEntityType = request.RelatedEntityType,
            RelatedEntityId = request.RelatedEntityId,
            UploadedByUserId = currentUser.UserId
        };

        _db.StorageFiles.Add(entity);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await SafeDeleteUploadedObjectAsync(key, cancellationToken);
            throw;
        }

        entity.UploadedByUser = currentUser.User;
        return MapResponse(entity);
    }

    public async Task<PaginatedResponse<StorageFileResponse>> GetFilesAsync(Guid userId, StorageFileQueryRequest request, CancellationToken cancellationToken = default)
    {
        var currentUser = await GetCurrentUserAsync(userId, cancellationToken);
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : request.PageSize > 100 ? 100 : request.PageSize;

        var query = _db.StorageFiles
            .AsNoTracking()
            .Include(x => x.UploadedByUser)
            .Where(x => !x.IsDeleted);

        query = ApplyAuthorizationFilter(query, currentUser);

        if (request.FileCategory.HasValue)
        {
            query = query.Where(x => x.FileCategory == request.FileCategory.Value);
        }

        if (request.UploadedByUserId.HasValue)
        {
            query = query.Where(x => x.UploadedByUserId == request.UploadedByUserId.Value);
        }

        if (request.RelatedEntityType.HasValue)
        {
            query = query.Where(x => x.RelatedEntityType == request.RelatedEntityType.Value);
        }

        if (request.RelatedEntityId.HasValue)
        {
            query = query.Where(x => x.RelatedEntityId == request.RelatedEntityId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.MimeType))
        {
            var mimeType = request.MimeType.Trim().ToLowerInvariant();
            query = query.Where(x => x.MimeType == mimeType);
        }

        if (request.CreatedFrom.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= request.CreatedFrom.Value);
        }

        if (request.CreatedTo.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= request.CreatedTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => EF.Functions.ILike(x.OriginalFileName, $"%{search}%"));
        }

        query = ApplySorting(query, request.SortBy, request.SortDirection);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResponse<StorageFileResponse>
        {
            Items = items.Select(MapResponse).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<StorageFileResponse> GetFileByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetAuthorizedFileAsync(userId, id, includeUploader: true, cancellationToken);
        return MapResponse(entity);
    }

    public async Task<StorageFileStreamResult> DownloadFileAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetAuthorizedFileAsync(userId, id, includeUploader: false, cancellationToken);
        var objectResult = await _storageProvider.DownloadAsync(entity.S3Key, cancellationToken);

        return new StorageFileStreamResult
        {
            Stream = objectResult.Stream,
            MimeType = entity.MimeType,
            FileName = entity.OriginalFileName,
            SizeInBytes = objectResult.ContentLength ?? entity.SizeInBytes
        };
    }

    public async Task<StorageFileStreamResult> PreviewFileAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetAuthorizedFileAsync(userId, id, includeUploader: false, cancellationToken);
        if (!CanPreview(entity.MimeType))
        {
            throw new InvalidOperationException("Bu dosya onizleme icin desteklenmiyor.");
        }

        var objectResult = await _storageProvider.DownloadAsync(entity.S3Key, cancellationToken);
        return new StorageFileStreamResult
        {
            Stream = objectResult.Stream,
            MimeType = entity.MimeType,
            FileName = entity.OriginalFileName,
            SizeInBytes = objectResult.ContentLength ?? entity.SizeInBytes
        };
    }

    public async Task<StorageAccessUrlResponse> GenerateAccessUrlAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetAuthorizedFileAsync(userId, id, includeUploader: false, cancellationToken);
        var expiresAt = DateTime.UtcNow.AddSeconds(_settings.PresignedUrlExpiresInSeconds);
        var url = _storageProvider.GeneratePresignedDownloadUrl(
            entity.S3Key,
            entity.OriginalFileName,
            TimeSpan.FromSeconds(_settings.PresignedUrlExpiresInSeconds));

        return new StorageAccessUrlResponse
        {
            Url = url,
            ExpiresAt = expiresAt
        };
    }

    public async Task<StorageFileResponse> UpdateMetadataAsync(Guid userId, Guid id, UpdateStorageFileMetadataRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await GetAuthorizedFileAsync(userId, id, includeUploader: true, cancellationToken);

        if (request.FileCategory.HasValue)
        {
            _fileValidationService.EnsureCategoryMatchesExistingFile(request.FileCategory.Value, entity.FileExtension, entity.MimeType);
            entity.FileCategory = request.FileCategory.Value;
        }

        if (request.Description != null)
        {
            entity.Description = NormalizeDescription(request.Description);
        }

        if (request.RelatedEntityType.HasValue)
        {
            EnsureRelatedEntityInput(request.RelatedEntityType.Value, request.RelatedEntityId);
            var currentUser = await GetCurrentUserAsync(userId, cancellationToken);
            await EnsureCanAttachToRelatedEntityAsync(currentUser, request.RelatedEntityType.Value, request.RelatedEntityId, cancellationToken);
            entity.RelatedEntityType = request.RelatedEntityType.Value;
            entity.RelatedEntityId = request.RelatedEntityId;
        }
        else if (request.RelatedEntityId.HasValue)
        {
            throw new InvalidOperationException("Iliskili kayit tipi olmadan iliskili kayit kimligi guncellenemez.");
        }

        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapResponse(entity);
    }

    public async Task<StorageFileResponse> ReplaceFileAsync(Guid userId, Guid id, ReplaceStorageFileRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await GetAuthorizedFileAsync(userId, id, includeUploader: true, cancellationToken);
        var formFile = request.File ?? throw new InvalidOperationException("Dosya gereklidir.");
        ValidatedStorageFile validatedFile;

        try
        {
            validatedFile = await _fileValidationService.ValidateAsync(
                formFile,
                entity.FileCategory,
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Dosya degistirme validasyonu basarisiz. UserId: {UserId}, StorageFileId: {StorageFileId}, FileName: {FileName}, MimeType: {MimeType}, Size: {Size}, Category: {Category}",
                userId,
                id,
                formFile.FileName,
                formFile.ContentType,
                formFile.Length,
                entity.FileCategory);
            throw;
        }

        var newStoredFileName = $"{Guid.NewGuid():N}-{validatedFile.SafeFileName}.{validatedFile.FileExtension}";
        var newKey = BuildStorageKey(validatedFile.FileCategory, newStoredFileName);
        var oldKey = entity.S3Key;

        await _storageProvider.UploadAsync(newKey, validatedFile.Content, validatedFile.MimeType, cancellationToken);

        entity.OriginalFileName = validatedFile.OriginalFileName;
        entity.StoredFileName = newStoredFileName;
        entity.SafeFileName = validatedFile.SafeFileName;
        entity.FileExtension = validatedFile.FileExtension;
        entity.MimeType = validatedFile.MimeType;
        entity.SizeInBytes = validatedFile.SizeInBytes;
        entity.S3Key = newKey;
        entity.BucketName = _settings.S3BucketName;
        entity.ReplacedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await SafeDeleteUploadedObjectAsync(newKey, cancellationToken);
            throw;
        }

        try
        {
            await _storageProvider.DeleteAsync(oldKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Eski S3 nesnesi silinemedi. StorageFileId: {StorageFileId}", entity.Id);
        }

        return MapResponse(entity);
    }

    public async Task DeleteFileAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetAuthorizedFileAsync(userId, id, includeUploader: false, cancellationToken);
        await _storageProvider.DeleteAsync(entity.S3Key, cancellationToken);

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<StorageFile> GetAuthorizedFileAsync(Guid userId, Guid id, bool includeUploader, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(userId, cancellationToken);
        IQueryable<StorageFile> query = _db.StorageFiles;
        if (includeUploader)
        {
            query = query.Include(x => x.UploadedByUser);
        }

        var entity = await query.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Dosya kaydi bulunamadi.");

        await EnsureCanAccessFileAsync(currentUser, entity, cancellationToken);
        return entity;
    }

    private async Task EnsureCanAccessFileAsync(CurrentStorageUser currentUser, StorageFile entity, CancellationToken cancellationToken)
    {
        if (entity.UploadedByUserId == currentUser.UserId)
        {
            return;
        }

        var canAccess = entity.RelatedEntityType switch
        {
            StorageRelatedEntityType.None => false,
            StorageRelatedEntityType.User => entity.RelatedEntityId == currentUser.UserId,
            StorageRelatedEntityType.AgentProfile => currentUser.Role == AgentRole && entity.RelatedEntityId == currentUser.AgentProfileId,
            StorageRelatedEntityType.SubcontractorProfile => currentUser.Role == SubcontractorRole && entity.RelatedEntityId == currentUser.SubcontractorProfileId,
            StorageRelatedEntityType.JobListing => await CanAccessJobListingAsync(currentUser, entity.RelatedEntityId, cancellationToken),
            StorageRelatedEntityType.Offer => await CanAccessOfferAsync(currentUser, entity.RelatedEntityId, cancellationToken),
            StorageRelatedEntityType.AssignedJob => await CanAccessAssignedJobAsync(currentUser, entity.RelatedEntityId, cancellationToken),
            StorageRelatedEntityType.Conversation => await CanAccessConversationAsync(currentUser, entity.RelatedEntityId, cancellationToken),
            _ => false
        };

        if (!canAccess)
        {
            throw new UnauthorizedAccessException("Bu dosyaya erisim yetkiniz yok.");
        }
    }

    private IQueryable<StorageFile> ApplyAuthorizationFilter(IQueryable<StorageFile> query, CurrentStorageUser currentUser)
    {
        if (currentUser.Role == AgentRole && currentUser.AgentProfileId.HasValue)
        {
            var agentProfileId = currentUser.AgentProfileId.Value;
            var userId = currentUser.UserId;
            return query.Where(x =>
                x.UploadedByUserId == userId ||
                (x.RelatedEntityType == StorageRelatedEntityType.User && x.RelatedEntityId == userId) ||
                (x.RelatedEntityType == StorageRelatedEntityType.AgentProfile && x.RelatedEntityId == agentProfileId) ||
                (x.RelatedEntityType == StorageRelatedEntityType.JobListing && _db.JobListings.Any(j => j.Id == x.RelatedEntityId && j.AgentId == agentProfileId)) ||
                (x.RelatedEntityType == StorageRelatedEntityType.Offer && _db.Offers.Any(o => o.Id == x.RelatedEntityId && o.JobListing.AgentId == agentProfileId)) ||
                (x.RelatedEntityType == StorageRelatedEntityType.AssignedJob && _db.AssignedJobs.Any(a => a.Id == x.RelatedEntityId && a.AgentId == agentProfileId)) ||
                (x.RelatedEntityType == StorageRelatedEntityType.Conversation && _db.Conversations.Any(c => c.Id == x.RelatedEntityId && c.AgentId == agentProfileId)));
        }

        if (currentUser.Role == SubcontractorRole && currentUser.SubcontractorProfileId.HasValue)
        {
            var subcontractorProfileId = currentUser.SubcontractorProfileId.Value;
            var userId = currentUser.UserId;
            return query.Where(x =>
                x.UploadedByUserId == userId ||
                (x.RelatedEntityType == StorageRelatedEntityType.User && x.RelatedEntityId == userId) ||
                (x.RelatedEntityType == StorageRelatedEntityType.SubcontractorProfile && x.RelatedEntityId == subcontractorProfileId) ||
                (x.RelatedEntityType == StorageRelatedEntityType.JobListing && (
                    _db.Offers.Any(o => o.JobId == x.RelatedEntityId && o.SubcontractorId == subcontractorProfileId) ||
                    _db.AssignedJobs.Any(a => a.JobId == x.RelatedEntityId && a.SubcontractorId == subcontractorProfileId))) ||
                (x.RelatedEntityType == StorageRelatedEntityType.Offer && _db.Offers.Any(o => o.Id == x.RelatedEntityId && o.SubcontractorId == subcontractorProfileId)) ||
                (x.RelatedEntityType == StorageRelatedEntityType.AssignedJob && _db.AssignedJobs.Any(a => a.Id == x.RelatedEntityId && a.SubcontractorId == subcontractorProfileId)) ||
                (x.RelatedEntityType == StorageRelatedEntityType.Conversation && _db.Conversations.Any(c => c.Id == x.RelatedEntityId && c.SubcontractorId == subcontractorProfileId)));
        }

        return query.Where(_ => false);
    }

    private IQueryable<StorageFile> ApplySorting(IQueryable<StorageFile> query, string? sortBy, string? sortDirection)
    {
        var descending = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        return (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "originalfilename" => descending ? query.OrderByDescending(x => x.OriginalFileName) : query.OrderBy(x => x.OriginalFileName),
            "mimetype" => descending ? query.OrderByDescending(x => x.MimeType) : query.OrderBy(x => x.MimeType),
            "sizeinbytes" => descending ? query.OrderByDescending(x => x.SizeInBytes) : query.OrderBy(x => x.SizeInBytes),
            "updatedat" => descending ? query.OrderByDescending(x => x.UpdatedAt) : query.OrderBy(x => x.UpdatedAt),
            _ => descending ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt)
        };
    }

    private async Task<CurrentStorageUser> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken)
            ?? throw new UnauthorizedAccessException("Kullanici bulunamadi.");

        Guid? agentProfileId = null;
        Guid? subcontractorProfileId = null;

        if (user.Role == AgentRole)
        {
            agentProfileId = await _db.AgentProfiles
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }
        else if (user.Role == SubcontractorRole)
        {
            subcontractorProfileId = await _db.SubcontractorProfiles
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (user.Role == AgentRole && !agentProfileId.HasValue)
        {
            throw new UnauthorizedAccessException("Acente profili bulunamadi.");
        }

        if (user.Role == SubcontractorRole && !subcontractorProfileId.HasValue)
        {
            throw new UnauthorizedAccessException("Taseron profili bulunamadi.");
        }

        if (user.Role != AgentRole && user.Role != SubcontractorRole)
        {
            throw new UnauthorizedAccessException("Bu hesap depolama modulu icin yetkili degil.");
        }

        return new CurrentStorageUser
        {
            UserId = user.Id,
            Role = user.Role,
            User = user,
            AgentProfileId = agentProfileId,
            SubcontractorProfileId = subcontractorProfileId
        };
    }

    private async Task EnsureCanAttachToRelatedEntityAsync(CurrentStorageUser currentUser, StorageRelatedEntityType relatedEntityType, Guid? relatedEntityId, CancellationToken cancellationToken)
    {
        if (relatedEntityType == StorageRelatedEntityType.None)
        {
            return;
        }

        if (!relatedEntityId.HasValue)
        {
            throw new InvalidOperationException("Iliskili kayit kimligi gereklidir.");
        }

        var isAllowed = relatedEntityType switch
        {
            StorageRelatedEntityType.User => relatedEntityId.Value == currentUser.UserId,
            StorageRelatedEntityType.AgentProfile => currentUser.Role == AgentRole && relatedEntityId.Value == currentUser.AgentProfileId,
            StorageRelatedEntityType.SubcontractorProfile => currentUser.Role == SubcontractorRole && relatedEntityId.Value == currentUser.SubcontractorProfileId,
            StorageRelatedEntityType.JobListing => await CanAccessJobListingAsync(currentUser, relatedEntityId, cancellationToken),
            StorageRelatedEntityType.Offer => await CanAccessOfferAsync(currentUser, relatedEntityId, cancellationToken),
            StorageRelatedEntityType.AssignedJob => await CanAccessAssignedJobAsync(currentUser, relatedEntityId, cancellationToken),
            StorageRelatedEntityType.Conversation => await CanAccessConversationAsync(currentUser, relatedEntityId, cancellationToken),
            _ => false
        };

        if (!isAllowed)
        {
            throw new UnauthorizedAccessException("Bu kayit ile dosya iliskilendirme yetkiniz yok.");
        }
    }

    private async Task<bool> CanAccessJobListingAsync(CurrentStorageUser currentUser, Guid? jobListingId, CancellationToken cancellationToken)
    {
        if (!jobListingId.HasValue)
        {
            return false;
        }

        if (currentUser.Role == AgentRole && currentUser.AgentProfileId.HasValue)
        {
            return await _db.JobListings.AnyAsync(
                x => x.Id == jobListingId.Value && x.AgentId == currentUser.AgentProfileId.Value,
                cancellationToken);
        }

        if (currentUser.Role == SubcontractorRole && currentUser.SubcontractorProfileId.HasValue)
        {
            return await _db.JobListings.AnyAsync(
                       x => x.Id == jobListingId.Value && x.Status == "active",
                       cancellationToken)
                   || await _db.Offers.AnyAsync(
                       x => x.JobId == jobListingId.Value && x.SubcontractorId == currentUser.SubcontractorProfileId.Value,
                       cancellationToken)
                   || await _db.AssignedJobs.AnyAsync(
                       x => x.JobId == jobListingId.Value && x.SubcontractorId == currentUser.SubcontractorProfileId.Value,
                       cancellationToken);
        }

        return false;
    }

    private async Task<bool> CanAccessOfferAsync(CurrentStorageUser currentUser, Guid? offerId, CancellationToken cancellationToken)
    {
        if (!offerId.HasValue)
        {
            return false;
        }

        if (currentUser.Role == AgentRole && currentUser.AgentProfileId.HasValue)
        {
            return await _db.Offers.AnyAsync(
                x => x.Id == offerId.Value && x.JobListing.AgentId == currentUser.AgentProfileId.Value,
                cancellationToken);
        }

        if (currentUser.Role == SubcontractorRole && currentUser.SubcontractorProfileId.HasValue)
        {
            return await _db.Offers.AnyAsync(
                x => x.Id == offerId.Value && x.SubcontractorId == currentUser.SubcontractorProfileId.Value,
                cancellationToken);
        }

        return false;
    }

    private async Task<bool> CanAccessAssignedJobAsync(CurrentStorageUser currentUser, Guid? assignedJobId, CancellationToken cancellationToken)
    {
        if (!assignedJobId.HasValue)
        {
            return false;
        }

        if (currentUser.Role == AgentRole && currentUser.AgentProfileId.HasValue)
        {
            return await _db.AssignedJobs.AnyAsync(
                x => x.Id == assignedJobId.Value && x.AgentId == currentUser.AgentProfileId.Value,
                cancellationToken);
        }

        if (currentUser.Role == SubcontractorRole && currentUser.SubcontractorProfileId.HasValue)
        {
            return await _db.AssignedJobs.AnyAsync(
                x => x.Id == assignedJobId.Value && x.SubcontractorId == currentUser.SubcontractorProfileId.Value,
                cancellationToken);
        }

        return false;
    }

    private async Task<bool> CanAccessConversationAsync(CurrentStorageUser currentUser, Guid? conversationId, CancellationToken cancellationToken)
    {
        if (!conversationId.HasValue)
        {
            return false;
        }

        if (currentUser.Role == AgentRole && currentUser.AgentProfileId.HasValue)
        {
            return await _db.Conversations.AnyAsync(
                x => x.Id == conversationId.Value && x.AgentId == currentUser.AgentProfileId.Value,
                cancellationToken);
        }

        if (currentUser.Role == SubcontractorRole && currentUser.SubcontractorProfileId.HasValue)
        {
            return await _db.Conversations.AnyAsync(
                x => x.Id == conversationId.Value && x.SubcontractorId == currentUser.SubcontractorProfileId.Value,
                cancellationToken);
        }

        return false;
    }

    private static string BuildStorageKey(StorageFileCategory category, string storedFileName)
    {
        var now = DateTime.UtcNow;
        var folder = category switch
        {
            StorageFileCategory.Image => "images",
            StorageFileCategory.Video => "videos",
            _ => "documents"
        };

        return $"storage/{folder}/{now:yyyy/MM}/{storedFileName}";
    }

    private static string? NormalizeDescription(string? description)
    {
        var normalized = description?.Trim();
        if (normalized != null && normalized.Length > 1000)
        {
            throw new InvalidOperationException("Aciklama en fazla 1000 karakter olabilir.");
        }

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static void EnsureRelatedEntityInput(StorageRelatedEntityType relatedEntityType, Guid? relatedEntityId)
    {
        if (relatedEntityType == StorageRelatedEntityType.None)
        {
            if (relatedEntityId.HasValue)
            {
                throw new InvalidOperationException("Iliskili kayit tipi yoksa iliskili kayit kimligi gonderilemez.");
            }

            return;
        }

        if (!relatedEntityId.HasValue)
        {
            throw new InvalidOperationException("Iliskili kayit kimligi zorunludur.");
        }
    }

    private async Task SafeDeleteUploadedObjectAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _storageProvider.DeleteAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Rollback sirasinda yuklenen S3 nesnesi silinemedi. Key: {Key}", key);
        }
    }

    private static bool CanPreview(string mimeType)
        => mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
           mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
           mimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);

    private static StorageFileResponse MapResponse(StorageFile entity)
    {
        return new StorageFileResponse
        {
            Id = entity.Id,
            OriginalFileName = entity.OriginalFileName,
            StoredFileName = entity.StoredFileName,
            MimeType = entity.MimeType,
            FileExtension = entity.FileExtension,
            SizeInBytes = entity.SizeInBytes,
            FileCategory = entity.FileCategory,
            Description = entity.Description,
            RelatedEntityType = entity.RelatedEntityType,
            RelatedEntityId = entity.RelatedEntityId,
            IsDeleted = entity.IsDeleted,
            DeletedAt = entity.DeletedAt,
            ReplacedAt = entity.ReplacedAt,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            UploadedBy = entity.UploadedByUser == null
                ? null
                : new StorageUploadedByResponse
                {
                    UserId = entity.UploadedByUser.Id,
                    Email = entity.UploadedByUser.Email,
                    Role = entity.UploadedByUser.Role
                },
            DownloadUrl = $"/api/storage/{entity.Id}/download",
            PreviewUrl = $"/api/storage/{entity.Id}/preview",
            AccessUrlEndpoint = $"/api/storage/{entity.Id}/access-url"
        };
    }

    private sealed class CurrentStorageUser
    {
        public Guid UserId { get; init; }
        public string Role { get; init; } = string.Empty;
        public Guid? AgentProfileId { get; init; }
        public Guid? SubcontractorProfileId { get; init; }
        public User User { get; init; } = null!;
    }
}
