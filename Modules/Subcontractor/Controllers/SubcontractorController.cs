using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portlink.Api.DTOs.Jobs;
using Portlink.Api.DTOs.Offers;
using Portlink.Api.Modules.Auth.Dtos;
using Portlink.Api.Modules.Common.Dtos;
using Portlink.Api.Modules.Storage.Dtos;
using Portlink.Api.Modules.Storage.Enums;
using Portlink.Api.Modules.Storage.Interfaces;
using Portlink.Api.Modules.Subcontractor.Interfaces;
using System.Security.Claims;

namespace Portlink.Api.Modules.Subcontractor;

[ApiController]
[Route("api/subcontractor")]
[Authorize(Roles = "subcontractor")]
public class SubcontractorController : ControllerBase
{
    private readonly ISubcontractorService _svc;
    private readonly IStorageService _storageService;

    public SubcontractorController(ISubcontractorService svc, IStorageService storageService)
    {
        _svc = svc;
        _storageService = storageService;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/subcontractor/profile
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var result = await _svc.GetSubcontractorProfileAsync(UserId);
        return Ok(ApiResponse<SubcontractorProfileResponse>.Ok(result));
    }

    // PATCH /api/subcontractor/profile
    [HttpPatch("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateSubcontractorProfileRequest req)
    {
        var result = await _svc.UpdateSubcontractorProfileAsync(UserId, req);
        return Ok(ApiResponse<SubcontractorProfileResponse>.Ok(result, "Profil başarıyla güncellendi."));
    }

    // POST /api/subcontractor/profile/logo
    [HttpPost("profile/logo")]
    public async Task<IActionResult> UploadLogo(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("Dosya seçilmedi."));
        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(ApiResponse.Fail("Dosya boyutu 5 MB'ı geçemez."));

        var ext = Path.GetExtension(file.FileName).ToLower();
        if (!new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(ext))
            return BadRequest(ApiResponse.Fail("Yalnızca JPG, PNG veya WebP yüklenebilir."));

        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "logos");
        Directory.CreateDirectory(uploadsDir);
        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);
        await using var stream = System.IO.File.Create(filePath);
        await file.CopyToAsync(stream);

        var logoUrl = $"/uploads/logos/{fileName}";
        await _svc.UploadLogoAsync(UserId, logoUrl);
        return Ok(ApiResponse<string>.Ok(logoUrl, "Logo başarıyla yüklendi."));
    }

    // GET /api/subcontractor/dashboard/stats
    [HttpGet("dashboard/stats")]
    public async Task<IActionResult> DashboardStats()
    {
        var result = await _svc.GetDashboardStatsAsync(UserId);
        return Ok(ApiResponse<SubcontractorDashboardStatsResponse>.Ok(result));
    }

    [HttpGet("jobs")]
    public async Task<IActionResult> ListJobs([FromQuery] string? category, [FromQuery] string? location,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _svc.ListActiveJobsAsync(category, location, search, page, pageSize);
        return Ok(ApiResponse<List<JobListingResponse>>.Ok(result));
    }

    [HttpGet("jobs/{id:guid}")]
    public async Task<IActionResult> GetJob(Guid id)
    {
        try
        {
            var result = await _svc.GetJobDetailAsync(id);
            return Ok(ApiResponse<JobListingDetailResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
    }

    [HttpPost("jobs/{id:guid}/offer")]
    public async Task<IActionResult> CreateOffer(Guid id, [FromBody] CreateOfferRequest req)
    {
        try
        {
            var result = await _svc.CreateOfferAsync(UserId, id, req);
            return StatusCode(201, ApiResponse<OfferResponse>.Ok(result, "Teklifiniz iletildi."));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        catch (InvalidOperationException ex) { return Conflict(ApiResponse.Fail(ex.Message)); }
    }

    [HttpPut("offers/{id:guid}/withdraw")]
    public async Task<IActionResult> WithdrawOffer(Guid id)
    {
        try
        {
            await _svc.WithdrawOfferAsync(UserId, id);
            return Ok(ApiResponse.Ok("Teklif geri cekildi."));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        catch (InvalidOperationException ex) { return Conflict(ApiResponse.Fail(ex.Message)); }
    }

    [HttpGet("offers")]
    public async Task<IActionResult> GetMyOffers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _svc.GetMyOffersAsync(UserId, page, pageSize);
        return Ok(ApiResponse<List<OfferResponse>>.Ok(result));
    }

    [HttpGet("active-jobs")]
    public async Task<IActionResult> GetActiveJobs([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _svc.GetActiveJobsAsync(UserId, page, pageSize);
        return Ok(ApiResponse<List<AssignedJobResponse>>.Ok(result));
    }

    [HttpGet("active-jobs/{id:guid}")]
    public async Task<IActionResult> GetActiveJobDetail(Guid id)
    {
        try
        {
            var result = await _svc.GetActiveJobDetailAsync(UserId, id);
            return Ok(ApiResponse<AssignedJobDetailResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
    }

    [HttpPut("active-jobs/{id:guid}")]
    public async Task<IActionResult> UpdateActiveJob(Guid id, [FromBody] UpdateAssignedJobRequest req)
    {
        try
        {
            var result = await _svc.UpdateActiveJobAsync(UserId, id, req);
            return Ok(ApiResponse<AssignedJobResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
    }

    [HttpPost("active-jobs/{id:guid}/reports")]
    public async Task<IActionResult> UploadReport(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("Dosya secilmedi."));
        if (file.Length > 50 * 1024 * 1024)
            return BadRequest(ApiResponse.Fail("Dosya boyutu 50 MB'i gecemez."));

        var storedFile = await _storageService.UploadFileAsync(UserId, new UploadStorageFileRequest
        {
            File = file,
            FileCategory = ResolveStorageCategory(file.FileName),
            RelatedEntityType = StorageRelatedEntityType.AssignedJob,
            RelatedEntityId = id
        }, cancellationToken);

        var ext = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
        try
        {
            var result = await _svc.UploadReportAsync(UserId, id, storedFile.OriginalFileName, storedFile.DownloadUrl, file.Length, ext);
            return StatusCode(201, ApiResponse<JobReportResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
    }

    [HttpGet("wallet")]
    public async Task<IActionResult> GetWallet()
    {
        var result = await _svc.GetWalletAsync(UserId);
        return Ok(ApiResponse<WalletResponse>.Ok(result));
    }

    // GET /api/subcontractor/agents/:id  (public agent profile)
    [HttpGet("agents/{id:guid}")]
    public async Task<IActionResult> GetAgentProfile(Guid id)
    {
        try
        {
            var result = await _svc.GetAgentPublicProfileAsync(UserId, id);
            return Ok(ApiResponse<AgentProfileResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
    }

    // POST /api/subcontractor/agents/:id/rate
    [HttpPost("agents/{id:guid}/rate")]
    public async Task<IActionResult> RateAgent(Guid id, [FromQuery] decimal rating)
    {
        try
        {
            await _svc.RateAgentAsync(UserId, id, rating);
            return Ok(ApiResponse.Ok("Puanlama kaydedildi."));
        }
        catch (InvalidOperationException ex) { return Conflict(ApiResponse.Fail(ex.Message)); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
    }
}
