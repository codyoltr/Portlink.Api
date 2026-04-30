using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portlink.Api.DTOs.Jobs;
using Portlink.Api.DTOs.Offers;
using Portlink.Api.Modules.Common.Dtos;
using Portlink.Api.Modules.Subcontractor.Interfaces;
using System.Security.Claims;

namespace Portlink.Api.Modules.Subcontractor;

[ApiController]
[Route("api/subcontractor")]
[Authorize(Roles = "subcontractor")]
public class SubcontractorController : ControllerBase
{
    private readonly ISubcontractorService _svc;

    public SubcontractorController(ISubcontractorService svc) => _svc = svc;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/subcontractor/dashboard/stats
    [HttpGet("dashboard/stats")]
    public async Task<IActionResult> DashboardStats()
    {
        var result = await _svc.GetDashboardStatsAsync(UserId);
        return Ok(ApiResponse<SubcontractorDashboardStatsResponse>.Ok(result));
    }

    // GET /api/subcontractor/jobs
    [HttpGet("jobs")]
    public async Task<IActionResult> ListJobs([FromQuery] string? category, [FromQuery] string? location,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _svc.ListActiveJobsAsync(category, location, search, page, pageSize);
        return Ok(ApiResponse<List<JobListingResponse>>.Ok(result));
    }

    // GET /api/subcontractor/jobs/:id
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

    // POST /api/subcontractor/jobs/:id/offer
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

    // PUT /api/subcontractor/offers/:id/withdraw
    [HttpPut("offers/{id:guid}/withdraw")]
    public async Task<IActionResult> WithdrawOffer(Guid id)
    {
        try
        {
            await _svc.WithdrawOfferAsync(UserId, id);
            return Ok(ApiResponse.Ok("Teklif geri çekildi."));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        catch (InvalidOperationException ex) { return Conflict(ApiResponse.Fail(ex.Message)); }
    }

    // GET /api/subcontractor/offers
    [HttpGet("offers")]
    public async Task<IActionResult> GetMyOffers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _svc.GetMyOffersAsync(UserId, page, pageSize);
        return Ok(ApiResponse<List<OfferResponse>>.Ok(result));
    }

    // GET /api/subcontractor/active-jobs
    [HttpGet("active-jobs")]
    public async Task<IActionResult> GetActiveJobs([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _svc.GetActiveJobsAsync(UserId, page, pageSize);
        return Ok(ApiResponse<List<AssignedJobResponse>>.Ok(result));
    }

    // GET /api/subcontractor/active-jobs/:id
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

    // PUT /api/subcontractor/active-jobs/:id  (progress / status update)
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

    // POST /api/subcontractor/active-jobs/:id/reports
    [HttpPost("active-jobs/{id:guid}/reports")]
    public async Task<IActionResult> UploadReport(Guid id, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("Dosya seçilmedi."));
        if (file.Length > 50 * 1024 * 1024)
            return BadRequest(ApiResponse.Fail("Dosya boyutu 50 MB'ı geçemez."));

        var uploads = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        Directory.CreateDirectory(uploads);
        var fileName = $"{Guid.NewGuid()}_{file.FileName}";
        var filePath = Path.Combine(uploads, fileName);
        await using var stream = System.IO.File.Create(filePath);
        await file.CopyToAsync(stream);

        var ext = Path.GetExtension(file.FileName).TrimStart('.').ToLower();
        try
        {
            var result = await _svc.UploadReportAsync(UserId, id, file.FileName, $"/uploads/{fileName}", file.Length, ext);
            return StatusCode(201, ApiResponse<JobReportResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
    }

    // GET /api/subcontractor/wallet
    [HttpGet("wallet")]
    public async Task<IActionResult> GetWallet()
    {
        var result = await _svc.GetWalletAsync(UserId);
        return Ok(ApiResponse<WalletResponse>.Ok(result));
    }
}
