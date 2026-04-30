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
    private readonly IAgentService _svc;

    public AgentController(IAgentService svc) => _svc = svc;

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
        var result = await _svc.GetJobDetailAsync(UserId, id);
        return Ok(ApiResponse<JobListingDetailResponse>.Ok(result));
    }

    // PUT /api/agent/jobs/:id
    [HttpPut("jobs/{id:guid}")]
    public async Task<IActionResult> UpdateJob(Guid id, [FromBody] UpdateJobListingRequest req)
    {
        var result = await _svc.UpdateJobAsync(UserId, id, req);
        return Ok(ApiResponse<JobListingResponse>.Ok(result));
    }

    // DELETE /api/agent/jobs/:id
    [HttpDelete("jobs/{id:guid}")]
    public async Task<IActionResult> DeleteJob(Guid id)
    {
        await _svc.DeleteJobAsync(UserId, id);
        return Ok(ApiResponse.Ok("İlan silindi."));
    }

    // GET /api/agent/jobs/:id/offers
    [HttpGet("jobs/{id:guid}/offers")]
    public async Task<IActionResult> GetJobOffers(Guid id)
    {
        var result = await _svc.GetJobOffersAsync(UserId, id);
        return Ok(ApiResponse<List<OfferResponse>>.Ok(result));
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
        var result = await _svc.UploadJobFileAsync(UserId, id, file.FileName, $"/uploads/{fileName}", file.Length, ext);
        return StatusCode(201, ApiResponse<JobFileResponse>.Ok(result));
    }

    // PUT /api/agent/offers/:offerId/accept
    [HttpPut("offers/{offerId:guid}/accept")]
    public async Task<IActionResult> AcceptOffer(Guid offerId)
    {
        var result = await _svc.AcceptOfferAsync(UserId, offerId);
        return Ok(ApiResponse<AssignedJobResponse>.Ok(result, "Teklif kabul edildi."));
    }

    // PUT /api/agent/offers/:offerId/reject
    [HttpPut("offers/{offerId:guid}/reject")]
    public async Task<IActionResult> RejectOffer(Guid offerId)
    {
        await _svc.RejectOfferAsync(UserId, offerId);
        return Ok(ApiResponse.Ok("Teklif reddedildi."));
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
        var result = await _svc.GetAssignedJobDetailAsync(UserId, id);
        return Ok(ApiResponse<AssignedJobDetailResponse>.Ok(result));
    }

    // POST /api/agent/assigned-jobs/:id/logs
    [HttpPost("assigned-jobs/{id:guid}/logs")]
    public async Task<IActionResult> AddJobLog(Guid id, [FromBody] AddJobLogRequest req)
    {
        var result = await _svc.AddJobLogAsync(UserId, id, req);
        return StatusCode(201, ApiResponse<JobLogResponse>.Ok(result));
    }

    // POST /api/agent/assigned-jobs/:id/request-report
    [HttpPost("assigned-jobs/{id:guid}/request-report")]
    public async Task<IActionResult> RequestReport(Guid id)
    {
        await _svc.RequestReportAsync(UserId, id);
        return Ok(ApiResponse.Ok("Rapor isteği gönderildi."));
    }

    // PUT /api/agent/assigned-jobs/:id  (complete)
    [HttpPut("assigned-jobs/{id:guid}")]
    public async Task<IActionResult> UpdateAssignedJob(Guid id, [FromBody] UpdateAssignedJobRequest req)
    {
        // Eğer status = completed ise tamamlama akışını çalıştır
        if (req.Status == "completed")
        {
            var result = await _svc.CompleteJobAsync(UserId, id);
            return Ok(ApiResponse<AssignedJobResponse>.Ok(result, "İş tamamlandı."));
        }
        return BadRequest(ApiResponse.Fail("Geçersiz işlem."));
    }

    // GET /api/agent/subcontractors
    [HttpGet("subcontractors")]
    public async Task<IActionResult> GetSubcontractors([FromQuery] string? search)
    {
        var result = await _svc.GetSubcontractorsAsync(search);
        return Ok(ApiResponse<List<Portlink.Api.DTOs.Auth.SubcontractorProfileResponse>>.Ok(result));
    }

    // POST /api/agent/subcontractors/:id/rate
    [HttpPost("subcontractors/{id:guid}/rate")]
    public async Task<IActionResult> RateSubcontractor(Guid id, [FromQuery] decimal rating)
    {
        await _svc.RateSubcontractorAsync(UserId, id, rating);
        return Ok(ApiResponse.Ok("Puanlama kaydedildi."));
    }

    // GET /api/agent/offers
    [HttpGet("offers")]
    public async Task<IActionResult> GetAllOffers()
    {
        var result = await _svc.GetAllOffersAsync(UserId);
        return Ok(ApiResponse<List<OfferResponse>>.Ok(result));
    }
}
