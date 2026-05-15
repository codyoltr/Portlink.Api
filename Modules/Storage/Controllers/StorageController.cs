using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Portlink.Api.Modules.Common.Dtos;
using Portlink.Api.Modules.Storage.Dtos;
using Portlink.Api.Modules.Storage.Interfaces;
using System.Security.Claims;

namespace Portlink.Api.Modules.Storage.Controllers;

[ApiController]
[Route("api/storage")]
[Authorize]
public class StorageController : ControllerBase
{
    private readonly IStorageService _storageService;

    public StorageController(IStorageService storageService)
    {
        _storageService = storageService;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    [RequestFormLimits(MultipartBodyLengthLimit = 104_857_600)]
    public async Task<IActionResult> Upload([FromForm] UploadStorageFileRequest request, CancellationToken cancellationToken)
    {
        var result = await _storageService.UploadFileAsync(UserId, request, cancellationToken);
        return StatusCode(201, ApiResponse<StorageFileResponse>.Ok(result, "Dosya basariyla yuklendi."));
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] StorageFileQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await _storageService.GetFilesAsync(UserId, request, cancellationToken);
        return Ok(ApiResponse<PaginatedResponse<StorageFileResponse>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _storageService.GetFileByIdAsync(UserId, id, cancellationToken);
        return Ok(ApiResponse<StorageFileResponse>.Ok(result));
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var result = await _storageService.DownloadFileAsync(UserId, id, cancellationToken);
        return File(result.Stream, result.MimeType, result.FileName, enableRangeProcessing: true);
    }

    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, CancellationToken cancellationToken)
    {
        var result = await _storageService.PreviewFileAsync(UserId, id, cancellationToken);
        Response.Headers[HeaderNames.ContentDisposition] = "inline";
        return new FileStreamResult(result.Stream, result.MimeType) { EnableRangeProcessing = true };
    }

    [HttpGet("{id:guid}/access-url")]
    public async Task<IActionResult> GetAccessUrl(Guid id, CancellationToken cancellationToken)
    {
        var result = await _storageService.GenerateAccessUrlAsync(UserId, id, cancellationToken);
        return Ok(ApiResponse<StorageAccessUrlResponse>.Ok(result));
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateMetadata(Guid id, [FromBody] UpdateStorageFileMetadataRequest request, CancellationToken cancellationToken)
    {
        var result = await _storageService.UpdateMetadataAsync(UserId, id, request, cancellationToken);
        return Ok(ApiResponse<StorageFileResponse>.Ok(result, "Dosya metaverisi guncellendi."));
    }

    [HttpPut("{id:guid}/file")]
    [RequestFormLimits(MultipartBodyLengthLimit = 104_857_600)]
    public async Task<IActionResult> ReplaceFile(Guid id, [FromForm] ReplaceStorageFileRequest request, CancellationToken cancellationToken)
    {
        var result = await _storageService.ReplaceFileAsync(UserId, id, request, cancellationToken);
        return Ok(ApiResponse<StorageFileResponse>.Ok(result, "Dosya basariyla degistirildi."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _storageService.DeleteFileAsync(UserId, id, cancellationToken);
        return Ok(ApiResponse.Ok("Dosya silindi."));
    }
}
