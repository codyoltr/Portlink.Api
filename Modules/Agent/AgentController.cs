using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portlink.Api.DTOs.Common;
using Portlink.Api.DTOs.Jobs;
using Portlink.Api.DTOs.Offers;
using System.Security.Claims;

namespace Portlink.Api.Modules.Agent;

[ApiController]
[Route("api/agent")]
[Authorize(Roles = "agent")]
public class AgentController : ControllerBase
{
    private readonly AgentService _svc;

    public AgentController(AgentService svc) => _svc = svc;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/agent/dashboard/stats
    [HttpGet("dashboard/stats")]
    public async Task<IActionResult> DashboardStats()
    {
        var result = await _svc.GetDashboardStatsAsync(UserId);
        return Ok(ApiResponse<AgentDashboardStatsResponse>.Ok(result));
    }

    // GET /api/agent/jobs
    [HttpGet("jobs")]
    public async Task<IActionResult> GetJobs([FromQuery] string? status, [FromQuery] string? category,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _svc.GetMyJobsAsync(UserId, status, category, page, pageSize);
        return Ok(ApiResponse<List<JobListingResponse>>.Ok(result));
    }

    // POST /api/agent/jobs
    [HttpPost("jobs")]
    public async Task<IActionResult> CreateJob([FromBody] CreateJobListingRequest req)
    {
        var result = await _svc.CreateJobAsync(UserId, req);
        return StatusCode(201, ApiResponse<JobListingResponse>.Ok(result));
    }

    // GET /api/agent/jobs/:id
    [HttpGet("jobs/{id:guid}")]
    public async Task<IActionResult> GetJob(Guid id)
    {
        try
        {
            var result = await _svc.GetJobDetailAsync(UserId, id);
            return Ok(ApiResponse<JobListingDetailResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
    }

    // PUT /api/agent/jobs/:id
    [HttpPut("jobs/{id:guid}")]
    public async Task<IActionResult> UpdateJob(Guid id, [FromBody] UpdateJobListingRequest req)
    {
        try
        {
            var result = await _svc.UpdateJobAsync(UserId, id, req);
            return Ok(ApiResponse<JobListingResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
    }

    // DELETE /api/agent/jobs/:id
    [HttpDelete("jobs/{id:guid}")]
    public async Task<IActionResult> DeleteJob(Guid id)
    {
        try
        {
            await _svc.DeleteJobAsync(UserId, id);
            return Ok(ApiResponse.Ok("İlan silindi."));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
    }

    // GET /api/agent/jobs/:id/offers
    [HttpGet("jobs/{id:guid}/offers")]
    public async Task<IActionResult> GetJobOffers(Guid id)
    {
        try
        {
            var result = await _svc.GetJobOffersAsync(UserId, id);
            return Ok(ApiResponse<List<OfferResponse>>.Ok(result));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
    }

    // POST /api/agent/jobs/:id/files
    [HttpPost("jobs/{id:guid}/files")]
    public async Task<IActionResult> UploadJobFile(Guid id, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("Dosya seçilmedi."));

        // Local storage (geliştirme için); üretimde S3 entegrasyonu yapılacak
        var uploads = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        Directory.CreateDirectory(uploads);
        var fileName = $"{Guid.NewGuid()}_{file.FileName}";
        var filePath = Path.Combine(uploads, fileName);
        await using var stream = System.IO.File.Create(filePath);
        await file.CopyToAsync(stream);

        var ext = Path.GetExtension(file.FileName).TrimStart('.').ToLower();
        try
        {
            var result = await _svc.UploadJobFileAsync(UserId, id, file.FileName, $"/uploads/{fileName}", file.Length, ext);
            return StatusCode(201, ApiResponse<JobFileResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
    }

    // PUT /api/agent/offers/:offerId/accept
    [HttpPut("offers/{offerId:guid}/accept")]
    public async Task<IActionResult> AcceptOffer(Guid offerId)
    {
        try
        {
            var result = await _svc.AcceptOfferAsync(UserId, offerId);
            return Ok(ApiResponse<AssignedJobResponse>.Ok(result, "Teklif kabul edildi."));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        catch (InvalidOperationException ex) { return Conflict(ApiResponse.Fail(ex.Message)); }
    }

    // PUT /api/agent/offers/:offerId/reject
    [HttpPut("offers/{offerId:guid}/reject")]
    public async Task<IActionResult> RejectOffer(Guid offerId)
    {
        try
        {
            await _svc.RejectOfferAsync(UserId, offerId);
            return Ok(ApiResponse.Ok("Teklif reddedildi."));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        catch (InvalidOperationException ex) { return Conflict(ApiResponse.Fail(ex.Message)); }
    }

    // GET /api/agent/assigned-jobs
    [HttpGet("assigned-jobs")]
    public async Task<IActionResult> GetAssignedJobs([FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _svc.GetAssignedJobsAsync(UserId, status, page, pageSize);
        return Ok(ApiResponse<List<AssignedJobResponse>>.Ok(result));
    }

    // GET /api/agent/assigned-jobs/:id
    [HttpGet("assigned-jobs/{id:guid}")]
    public async Task<IActionResult> GetAssignedJobDetail(Guid id)
    {
        try
        {
            var result = await _svc.GetAssignedJobDetailAsync(UserId, id);
            return Ok(ApiResponse<AssignedJobDetailResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
    }

    // POST /api/agent/assigned-jobs/:id/logs
    [HttpPost("assigned-jobs/{id:guid}/logs")]
    public async Task<IActionResult> AddJobLog(Guid id, [FromBody] AddJobLogRequest req)
    {
        try
        {
            var result = await _svc.AddJobLogAsync(UserId, id, req);
            return StatusCode(201, ApiResponse<JobLogResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
    }

    // POST /api/agent/assigned-jobs/:id/request-report
    [HttpPost("assigned-jobs/{id:guid}/request-report")]
    public async Task<IActionResult> RequestReport(Guid id)
    {
        try
        {
            await _svc.RequestReportAsync(UserId, id);
            return Ok(ApiResponse.Ok("Rapor isteği gönderildi."));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
    }

    // PUT /api/agent/assigned-jobs/:id  (complete)
    [HttpPut("assigned-jobs/{id:guid}")]
    public async Task<IActionResult> UpdateAssignedJob(Guid id, [FromBody] UpdateAssignedJobRequest req)
    {
        try
        {
            // Eğer status = completed ise tamamlama akışını çalıştır
            if (req.Status == "completed")
            {
                var result = await _svc.CompleteJobAsync(UserId, id);
                return Ok(ApiResponse<AssignedJobResponse>.Ok(result, "İş tamamlandı."));
            }
            return BadRequest(ApiResponse.Fail("Geçersiz işlem."));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        catch (InvalidOperationException ex) { return Conflict(ApiResponse.Fail(ex.Message)); }
    }
}
