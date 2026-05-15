using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portlink.Api.DTOs.Agents;
using Portlink.Api.DTOs.Jobs;
using Portlink.Api.DTOs.Offers;
using Portlink.Api.Modules.Auth.Dtos;
using Portlink.Api.Modules.Common.Dtos;
using Portlink.Api.Modules.Storage.Dtos;
using Portlink.Api.Modules.Storage.Enums;
using Portlink.Api.Modules.Storage.Interfaces;
using System.Security.Claims;

namespace Portlink.Api.Modules.Agent;

[ApiController]
[Route("api/agent")]
[Authorize(Roles = "agent")]
public class AgentController : ControllerBase
{
    private readonly IAgentService _svc;
    private readonly IStorageService _storageService;

    public AgentController(IAgentService svc, IStorageService storageService)
    {
        _svc = svc;
        _storageService = storageService;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var result = await _svc.GetProfileAsync(UserId);
        return Ok(ApiResponse<AgentProfileResponse>.Ok(result));
    }

    [HttpPatch("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateAgencyProfileRequest req)
    {
        var result = await _svc.UpdateProfileAsync(UserId, req);
        return Ok(ApiResponse<AgentProfileResponse>.Ok(result, "Profil basariyla guncellendi."));
    }

    // POST /api/agent/profile/logo
    [HttpPost("profile/logo")]
    public async Task<IActionResult> UploadLogo(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("Dosya seçilmedi."));

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(ApiResponse.Fail("Dosya boyutu 5 MB'ı aşamaz."));

        var ext = Path.GetExtension(file.FileName).TrimStart('.').ToLower();
        if (ext is not ("jpg" or "jpeg" or "png" or "webp"))
            return BadRequest(ApiResponse.Fail("Yalnızca JPG, PNG veya WebP dosyaları kabul edilir."));

        var logosDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "logos");
        Directory.CreateDirectory(logosDir);
        var fileName = $"{Guid.NewGuid()}.{ext}";
        var filePath = Path.Combine(logosDir, fileName);
        await using var stream = System.IO.File.Create(filePath);
        await file.CopyToAsync(stream);

        var logoUrl = $"/uploads/logos/{fileName}";
        var result = await _svc.UploadLogoAsync(UserId, logoUrl);
        return Ok(ApiResponse<string>.Ok(result, "Logo güncellendi."));
    }

    // GET /api/agent/dashboard/stats
    [HttpGet("dashboard/stats")]
    public async Task<IActionResult> DashboardStats()
    {
        var result = await _svc.GetDashboardStatsAsync(UserId);
        return Ok(ApiResponse<AgentDashboardStatsResponse>.Ok(result));
    }

    [HttpGet("jobs")]
    public async Task<IActionResult> GetJobs([FromQuery] string? status, [FromQuery] string? category,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _svc.GetMyJobsAsync(UserId, status, category, page, pageSize);
        return Ok(ApiResponse<List<JobListingResponse>>.Ok(result));
    }

    [HttpGet("marketplace-jobs")]
    public async Task<IActionResult> ListMarketplaceJobs([FromQuery] string? category, [FromQuery] string? location,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _svc.ListMarketplaceJobsAsync(category, location, search, page, pageSize);
        return Ok(ApiResponse<List<JobListingResponse>>.Ok(result));
    }

    [HttpPost("jobs")]
    public async Task<IActionResult> CreateJob([FromBody] CreateJobListingRequest req)
    {
        var result = await _svc.CreateJobAsync(UserId, req);
        return StatusCode(201, ApiResponse<JobListingResponse>.Ok(result));
    }

    [HttpGet("jobs/{id:guid}")]
    public async Task<IActionResult> GetJob(Guid id)
    {
        var result = await _svc.GetJobDetailAsync(UserId, id);
        return Ok(ApiResponse<JobListingDetailResponse>.Ok(result));
    }

    [HttpPut("jobs/{id:guid}")]
    public async Task<IActionResult> UpdateJob(Guid id, [FromBody] UpdateJobListingRequest req)
    {
        var result = await _svc.UpdateJobAsync(UserId, id, req);
        return Ok(ApiResponse<JobListingResponse>.Ok(result));
    }

    [HttpDelete("jobs/{id:guid}")]
    public async Task<IActionResult> DeleteJob(Guid id)
    {
        await _svc.DeleteJobAsync(UserId, id);
        return Ok(ApiResponse.Ok("Ilan silindi."));
    }

    [HttpGet("jobs/{id:guid}/offers")]
    public async Task<IActionResult> GetJobOffers(Guid id)
    {
        var result = await _svc.GetJobOffersAsync(UserId, id);
        return Ok(ApiResponse<List<OfferResponse>>.Ok(result));
    }

    [HttpPost("jobs/{id:guid}/files")]
    public async Task<IActionResult> UploadJobFile(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("Dosya secilmedi."));

        var storedFile = await _storageService.UploadFileAsync(UserId, new UploadStorageFileRequest
        {
            File = file,
            FileCategory = ResolveStorageCategory(file.FileName),
            RelatedEntityType = StorageRelatedEntityType.JobListing,
            RelatedEntityId = id
        }, cancellationToken);

        var ext = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
        var result = await _svc.UploadJobFileAsync(UserId, id, storedFile.OriginalFileName, storedFile.DownloadUrl, storedFile.Id, file.Length, ext);
        return StatusCode(201, ApiResponse<JobFileResponse>.Ok(result));
    }

    [HttpPost("jobs/{id:guid}/listing-image")]
    public async Task<IActionResult> UploadJobListingImage(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("Dosya secilmedi."));

        var storedFile = await _storageService.UploadFileAsync(UserId, new UploadStorageFileRequest
        {
            File = file,
            FileCategory = StorageFileCategory.Image,
            RelatedEntityType = StorageRelatedEntityType.JobListing,
            RelatedEntityId = id
        }, cancellationToken);

        var previousStorageFileId = await _svc.SetJobListingImageAsync(UserId, id, storedFile.Id);

        if (previousStorageFileId.HasValue && previousStorageFileId.Value != storedFile.Id)
        {
            try
            {
                await _storageService.DeleteFileAsync(UserId, previousStorageFileId.Value, cancellationToken);
            }
            catch
            {
                // Listing image update already succeeded; old file cleanup is best-effort.
            }
        }

        return Ok(ApiResponse<StorageFileResponse>.Ok(storedFile, "Ilan fotografi guncellendi."));
    }

    [HttpPut("offers/{offerId:guid}/accept")]
    public async Task<IActionResult> AcceptOffer(Guid offerId)
    {
        var result = await _svc.AcceptOfferAsync(UserId, offerId);
        return Ok(ApiResponse<AssignedJobResponse>.Ok(result, "Teklif kabul edildi."));
    }

    [HttpPut("offers/{offerId:guid}/reject")]
    public async Task<IActionResult> RejectOffer(Guid offerId)
    {
        await _svc.RejectOfferAsync(UserId, offerId);
        return Ok(ApiResponse.Ok("Teklif reddedildi."));
    }

    [HttpGet("assigned-jobs")]
    public async Task<IActionResult> GetAssignedJobs([FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _svc.GetAssignedJobsAsync(UserId, status, page, pageSize);
        return Ok(ApiResponse<List<AssignedJobResponse>>.Ok(result));
    }

    [HttpGet("assigned-jobs/{id:guid}")]
    public async Task<IActionResult> GetAssignedJobDetail(Guid id)
    {
        var result = await _svc.GetAssignedJobDetailAsync(UserId, id);
        return Ok(ApiResponse<AssignedJobDetailResponse>.Ok(result));
    }

    [HttpPost("assigned-jobs/{id:guid}/logs")]
    public async Task<IActionResult> AddJobLog(Guid id, [FromBody] AddJobLogRequest req)
    {
        var result = await _svc.AddJobLogAsync(UserId, id, req);
        return StatusCode(201, ApiResponse<JobLogResponse>.Ok(result));
    }

    // PUT /api/agent/assigned-jobs/:id/logs/:logId/approve
    [HttpPut("assigned-jobs/{id:guid}/logs/{logId:guid}/approve")]
    public async Task<IActionResult> ApproveJobLog(Guid id, Guid logId, [FromBody] ReviewJobLogRequest req)
    {
        var result = await _svc.ApproveJobLogAsync(UserId, id, logId, req);
        return Ok(ApiResponse<JobLogResponse>.Ok(result, "Süreç logu onaylandı."));
    }

    // PUT /api/agent/assigned-jobs/:id/logs/:logId/reject
    [HttpPut("assigned-jobs/{id:guid}/logs/{logId:guid}/reject")]
    public async Task<IActionResult> RejectJobLog(Guid id, Guid logId, [FromBody] ReviewJobLogRequest req)
    {
        var result = await _svc.RejectJobLogAsync(UserId, id, logId, req);
        return Ok(ApiResponse<JobLogResponse>.Ok(result, "Süreç logu reddedildi."));
    }

    // POST /api/agent/assigned-jobs/:id/request-report
    [HttpPost("assigned-jobs/{id:guid}/request-report")]
    public async Task<IActionResult> RequestReport(Guid id)
    {
        await _svc.RequestReportAsync(UserId, id);
        return Ok(ApiResponse.Ok("Rapor istegi gonderildi."));
    }

    [HttpPut("assigned-jobs/{id:guid}")]
    public async Task<IActionResult> UpdateAssignedJob(Guid id, [FromBody] UpdateAssignedJobRequest req)
    {
        if (req.Status == "completed")
        {
            var result = await _svc.CompleteJobAsync(UserId, id);
            return Ok(ApiResponse<AssignedJobResponse>.Ok(result, "Is tamamlandi."));
        }

        return BadRequest(ApiResponse.Fail("Gecersiz islem."));
    }

    [HttpGet("subcontractors")]
    public async Task<IActionResult> GetSubcontractors([FromQuery] string? search)
    {
        var result = await _svc.GetSubcontractorsAsync(search);
        return Ok(ApiResponse<List<Portlink.Api.Modules.Auth.Dtos.SubcontractorProfileResponse>>.Ok(result));
    }

    // GET /api/agent/subcontractors/:id
    [HttpGet("subcontractors/{id:guid}")]
    public async Task<IActionResult> GetSubcontractorById(Guid id)
    {
        try
        {
            var result = await _svc.GetSubcontractorByIdAsync(UserId, id);
            return Ok(ApiResponse<Portlink.Api.Modules.Auth.Dtos.SubcontractorProfileResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
    }

    // POST /api/agent/subcontractors/:id/rate
    [HttpPost("subcontractors/{id:guid}/rate")]
    public async Task<IActionResult> RateSubcontractor(Guid id, [FromQuery] decimal rating)
    {
        try
        {
            await _svc.RateSubcontractorAsync(UserId, id, rating);
            return Ok(ApiResponse.Ok("Puanlama kaydedildi."));
        }
        catch (InvalidOperationException ex) { return Conflict(ApiResponse.Fail(ex.Message)); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
    }

    [HttpGet("offers")]
    public async Task<IActionResult> GetAllOffers([FromQuery] AgentOffersQueryRequest request)
    {
        var result = await _svc.GetAllOffersAsync(UserId, request);
        return Ok(ApiResponse<AgentOffersDashboardResponse>.Ok(result));
    }

    [HttpGet("offers/{offerId:guid}")]
    public async Task<IActionResult> GetOfferDetail(Guid offerId)
    {
        var result = await _svc.GetOfferDetailAsync(UserId, offerId);
        return Ok(ApiResponse<AgentOfferDetailResponse>.Ok(result));
    }

    private static StorageFileCategory ResolveStorageCategory(string fileName)
    {
        var extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();

        return extension switch
        {
            "jpg" or "jpeg" or "png" => StorageFileCategory.Image,
            "mp4" or "mov" or "webm" => StorageFileCategory.Video,
            _ => StorageFileCategory.Document
        };
    }
}
