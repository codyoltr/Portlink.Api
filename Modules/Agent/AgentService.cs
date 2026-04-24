using Microsoft.EntityFrameworkCore;
using Portlink.Api.Data;
using Portlink.Api.DTOs.Jobs;
using Portlink.Api.DTOs.Offers;
using Portlink.Api.Entities;

namespace Portlink.Api.Modules.Agent;

public class AgentService : IAgentService
{
    private readonly AppDbContext _db;

    public AgentService(AppDbContext db)
    {
        _db = db;
    }

    // ─── DASHBOARD ───────────────────────────────────────────────────────────

    public async Task<AgentDashboardStatsResponse> GetDashboardStatsAsync(Guid userId)
    {
        var agent = await GetAgentProfileAsync(userId);
        return new AgentDashboardStatsResponse
        {
            ActiveListings = await _db.JobListings.CountAsync(j => j.AgentId == agent.Id && j.Status == "active"),
            TotalOffers   = await _db.Offers.CountAsync(o => o.JobListing.AgentId == agent.Id && o.Status == "pending"),
            ActiveJobs    = await _db.AssignedJobs.CountAsync(a => a.AgentId == agent.Id && a.Status != "completed"),
            CompletedJobs = await _db.AssignedJobs.CountAsync(a => a.AgentId == agent.Id && a.Status == "completed")
        };
    }

    // ─── JOB LISTINGS ────────────────────────────────────────────────────────

    public async Task<List<JobListingResponse>> GetMyJobsAsync(Guid userId, string? status, string? category, int page, int pageSize)
    {
        var agent = await GetAgentProfileAsync(userId);
        var query = _db.JobListings.Include(j => j.Agent).Where(j => j.AgentId == agent.Id);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(j => j.Status == status);
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(j => j.Category == category);
        var jobs = await query.OrderByDescending(j => j.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return jobs.Select(MapJobListing).ToList();
    }

    public async Task<JobListingDetailResponse> GetJobDetailAsync(Guid userId, Guid jobId)
    {
        var agent = await GetAgentProfileAsync(userId);
        var job = await _db.JobListings.Include(j => j.Agent).Include(j => j.JobFiles)
            .FirstOrDefaultAsync(j => j.Id == jobId && j.AgentId == agent.Id)
            ?? throw new KeyNotFoundException("İlan bulunamadı.");
        var r = new JobListingDetailResponse
        {
            Id = job.Id, AgentId = job.AgentId, AgentCompanyName = job.Agent.CompanyName,
            AgentLogoUrl = job.Agent.LogoUrl, Title = job.Title, ListingType = job.ListingType,
            ShipName = job.ShipName, PortCode = job.PortCode, PortName = job.PortName, Location = job.Location,
            Category = job.Category, SelectedServices = job.SelectedServices?.ToList() ?? new(),
            Eta = job.Eta, NeedText = job.NeedText, BudgetMin = job.BudgetMin, BudgetMax = job.BudgetMax,
            Currency = job.Currency, Status = job.Status, OfferCount = job.OfferCount, Deadline = job.Deadline,
            CreatedAt = job.CreatedAt,
            Files = job.JobFiles.Select(f => new JobFileResponse
            {
                Id = f.Id, FileName = f.FileName, FileUrl = f.FileUrl,
                FileSize = f.FileSize, FileType = f.FileType, CreatedAt = f.CreatedAt
            }).ToList()
        };
        return r;
    }

    public async Task<JobListingResponse> CreateJobAsync(Guid userId, CreateJobListingRequest req)
    {
        var agent = await GetAgentProfileAsync(userId);
        var job = new JobListing
        {
            AgentId = agent.Id, Title = req.Title.Trim(), ListingType = req.ListingType,
            ShipName = req.ShipName?.Trim(), PortCode = req.PortCode?.Trim(), PortName = req.PortName?.Trim(),
            Location = req.Location?.Trim(), Category = req.Category.Trim(),
            SelectedServices = req.SelectedServices ?? new List<string>(),
            Eta = req.Eta, NeedText = req.NeedText?.Trim(), BudgetMin = req.BudgetMin,
            BudgetMax = req.BudgetMax, Currency = req.Currency, Deadline = req.Deadline, Status = "active"
        };
        _db.JobListings.Add(job);
        agent.TotalJobs++;
        await _db.SaveChangesAsync();
        job.Agent = agent;
        return MapJobListing(job);
    }

    public async Task<JobListingResponse> UpdateJobAsync(Guid userId, Guid jobId, UpdateJobListingRequest req)
    {
        var agent = await GetAgentProfileAsync(userId);
        var job = await _db.JobListings.Include(j => j.Agent)
            .FirstOrDefaultAsync(j => j.Id == jobId && j.AgentId == agent.Id)
            ?? throw new KeyNotFoundException("İlan bulunamadı.");
        if (req.Title != null) job.Title = req.Title.Trim();
        if (req.ShipName != null) job.ShipName = req.ShipName.Trim();
        if (req.PortCode != null) job.PortCode = req.PortCode.Trim();
        if (req.Location != null) job.Location = req.Location.Trim();
        if (req.Category != null) job.Category = req.Category.Trim();
        if (req.SelectedServices != null) job.SelectedServices = req.SelectedServices;
        if (req.Eta.HasValue) job.Eta = req.Eta;
        if (req.NeedText != null) job.NeedText = req.NeedText.Trim();
        if (req.BudgetMin.HasValue) job.BudgetMin = req.BudgetMin;
        if (req.BudgetMax.HasValue) job.BudgetMax = req.BudgetMax;
        if (req.Currency != null) job.Currency = req.Currency;
        if (req.Deadline.HasValue) job.Deadline = req.Deadline;
        if (req.Status != null) job.Status = req.Status;
        job.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return MapJobListing(job);
    }

    public async Task DeleteJobAsync(Guid userId, Guid jobId)
    {
        var agent = await GetAgentProfileAsync(userId);
        var job = await _db.JobListings.FirstOrDefaultAsync(j => j.Id == jobId && j.AgentId == agent.Id)
            ?? throw new KeyNotFoundException("İlan bulunamadı.");
        _db.JobListings.Remove(job);
        await _db.SaveChangesAsync();
    }

    // ─── OFFERS ──────────────────────────────────────────────────────────────

    public async Task<List<OfferResponse>> GetJobOffersAsync(Guid userId, Guid jobId)
    {
        var agent = await GetAgentProfileAsync(userId);
        if (!await _db.JobListings.AnyAsync(j => j.Id == jobId && j.AgentId == agent.Id))
            throw new KeyNotFoundException("İlan bulunamadı.");
        return await _db.Offers.Include(o => o.Subcontractor).Include(o => o.JobListing)
            .Where(o => o.JobId == jobId).OrderByDescending(o => o.CreatedAt)
            .Select(o => new OfferResponse
            {
                Id = o.Id, JobId = o.JobId, JobTitle = o.JobListing.Title,
                SubcontractorId = o.SubcontractorId, SubcontractorCompanyName = o.Subcontractor.CompanyName,
                SubcontractorLogoUrl = o.Subcontractor.LogoUrl, SubcontractorRating = o.Subcontractor.Rating,
                Price = o.Price, Currency = o.Currency, EstimatedDays = o.EstimatedDays,
                CoverNote = o.CoverNote, Status = o.Status, CreatedAt = o.CreatedAt
            }).ToListAsync();
    }

    public async Task<AssignedJobResponse> AcceptOfferAsync(Guid userId, Guid offerId)
    {
        var agent = await GetAgentProfileAsync(userId);
        var offer = await _db.Offers.Include(o => o.JobListing)
            .FirstOrDefaultAsync(o => o.Id == offerId && o.JobListing.AgentId == agent.Id)
            ?? throw new KeyNotFoundException("Teklif bulunamadı.");
        if (offer.Status != "pending") throw new InvalidOperationException("Bu teklif zaten işleme alınmış.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        offer.Status = "accepted"; offer.UpdatedAt = DateTime.UtcNow;
        var others = await _db.Offers.Where(o => o.JobId == offer.JobId && o.Id != offerId && o.Status == "pending").ToListAsync();
        others.ForEach(o => { o.Status = "rejected"; o.UpdatedAt = DateTime.UtcNow; });
        offer.JobListing.Status = "reviewing"; offer.JobListing.UpdatedAt = DateTime.UtcNow;

        var assigned = new AssignedJob
        {
            JobId = offer.JobId, OfferId = offer.Id, AgentId = agent.Id,
            SubcontractorId = offer.SubcontractorId, Status = "planning", Progress = 0
        };
        _db.AssignedJobs.Add(assigned);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        _db.JobLogs.Add(new JobLog
        {
            AssignedJobId = assigned.Id, CreatedBy = userId,
            Title = "İş başlatıldı", Description = "Teklif kabul edildi, iş planlanıyor."
        });

        // Taşeronun user'ına bildirim
        var subUserId = await _db.SubcontractorProfiles.Where(s => s.Id == offer.SubcontractorId).Select(s => s.UserId).FirstAsync();
        _db.Notifications.Add(new Notification
        {
            UserId = subUserId, Type = "OFFER_ACCEPTED", Title = "Teklifiniz Kabul Edildi",
            Body = $"{offer.JobListing.Title} ilanı için teklifiniz kabul edildi.",
            Data = System.Text.Json.JsonSerializer.Serialize(new { assignedJobId = assigned.Id })
        });
        await _db.SaveChangesAsync();

        await _db.Entry(assigned).Reference(a => a.JobListing).LoadAsync();
        await _db.Entry(assigned).Reference(a => a.Agent).LoadAsync();
        await _db.Entry(assigned).Reference(a => a.Subcontractor).LoadAsync();
        return MapAssignedJob(assigned);
    }

    public async Task RejectOfferAsync(Guid userId, Guid offerId)
    {
        var agent = await GetAgentProfileAsync(userId);
        var offer = await _db.Offers.Include(o => o.JobListing)
            .FirstOrDefaultAsync(o => o.Id == offerId && o.JobListing.AgentId == agent.Id)
            ?? throw new KeyNotFoundException("Teklif bulunamadı.");
        if (offer.Status != "pending") throw new InvalidOperationException("Bu teklif zaten işleme alınmış.");
        offer.Status = "rejected"; offer.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ─── ASSIGNED JOBS ───────────────────────────────────────────────────────

    public async Task<List<AssignedJobResponse>> GetAssignedJobsAsync(Guid userId, string? status, int page, int pageSize)
    {
        var agent = await GetAgentProfileAsync(userId);
        var query = _db.AssignedJobs.Include(a => a.JobListing).Include(a => a.Agent).Include(a => a.Subcontractor)
            .Where(a => a.AgentId == agent.Id);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(a => a.Status == status);
        var list = await query.OrderByDescending(a => a.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return list.Select(MapAssignedJob).ToList();
    }

    public async Task<AssignedJobDetailResponse> GetAssignedJobDetailAsync(Guid userId, Guid id)
    {
        var agent = await GetAgentProfileAsync(userId);
        var a = await _db.AssignedJobs
            .Include(x => x.JobListing).Include(x => x.Agent).Include(x => x.Subcontractor)
            .Include(x => x.JobLogs).Include(x => x.JobReports)
            .FirstOrDefaultAsync(x => x.Id == id && x.AgentId == agent.Id)
            ?? throw new KeyNotFoundException("Atanmış iş bulunamadı.");
        return new AssignedJobDetailResponse
        {
            Id = a.Id, JobId = a.JobId, JobTitle = a.JobListing.Title,
            AgentCompanyName = a.Agent.CompanyName, SubcontractorCompanyName = a.Subcontractor.CompanyName,
            Progress = a.Progress, Status = a.Status, StartDate = a.StartDate, DueDate = a.DueDate,
            CompletedAt = a.CompletedAt, CreatedAt = a.CreatedAt,
            Logs = a.JobLogs.OrderByDescending(l => l.CreatedAt).Select(l => new JobLogResponse { Id = l.Id, Title = l.Title, Description = l.Description, CreatedAt = l.CreatedAt }).ToList(),
            Reports = a.JobReports.OrderByDescending(r => r.CreatedAt).Select(r => new JobReportResponse { Id = r.Id, FileName = r.FileName, FileUrl = r.FileUrl, FileSize = r.FileSize, FileType = r.FileType, CreatedAt = r.CreatedAt }).ToList()
        };
    }

    public async Task<JobLogResponse> AddJobLogAsync(Guid userId, Guid assignedJobId, AddJobLogRequest req)
    {
        var agent = await GetAgentProfileAsync(userId);
        var assigned = await _db.AssignedJobs.FirstOrDefaultAsync(a => a.Id == assignedJobId && a.AgentId == agent.Id)
            ?? throw new KeyNotFoundException("Atanmış iş bulunamadı.");
        var log = new JobLog { AssignedJobId = assigned.Id, CreatedBy = userId, Title = req.Title.Trim(), Description = req.Description?.Trim() };
        _db.JobLogs.Add(log);
        await _db.SaveChangesAsync();
        return new JobLogResponse { Id = log.Id, Title = log.Title, Description = log.Description, CreatedAt = log.CreatedAt };
    }

    public async Task RequestReportAsync(Guid userId, Guid assignedJobId)
    {
        var agent = await GetAgentProfileAsync(userId);
        var assigned = await _db.AssignedJobs.FirstOrDefaultAsync(a => a.Id == assignedJobId && a.AgentId == agent.Id)
            ?? throw new KeyNotFoundException("Atanmış iş bulunamadı.");
        var subUserId = await _db.SubcontractorProfiles.Where(s => s.Id == assigned.SubcontractorId).Select(s => s.UserId).FirstAsync();
        _db.Notifications.Add(new Notification
        {
            UserId = subUserId, Type = "REPORT_REQUESTED", Title = "Rapor Talebi",
            Body = "Acente tarafından rapor yüklemeniz talep edildi.",
            Data = System.Text.Json.JsonSerializer.Serialize(new { assignedJobId })
        });
        await _db.SaveChangesAsync();
    }

    public async Task<AssignedJobResponse> CompleteJobAsync(Guid userId, Guid assignedJobId)
    {
        var agent = await GetAgentProfileAsync(userId);
        var a = await _db.AssignedJobs.Include(x => x.JobListing).Include(x => x.Agent).Include(x => x.Subcontractor).Include(x => x.Offer)
            .FirstOrDefaultAsync(x => x.Id == assignedJobId && x.AgentId == agent.Id)
            ?? throw new KeyNotFoundException("Atanmış iş bulunamadı.");
        if (a.Status == "completed") throw new InvalidOperationException("İş zaten tamamlandı.");
        await using var tx = await _db.Database.BeginTransactionAsync();
        a.Status = "completed"; a.CompletedAt = DateTime.UtcNow; a.UpdatedAt = DateTime.UtcNow;
        a.JobListing.Status = "completed"; a.JobListing.UpdatedAt = DateTime.UtcNow;
        a.Subcontractor.TotalCompleted++;
        if (a.Offer != null)
        {
            _db.WalletTransactions.Add(new WalletTransaction
            {
                SubcontractorId = a.SubcontractorId, AssignedJobId = a.Id, Type = "earning",
                Amount = a.Offer.Price, Currency = a.Offer.Currency, Status = "pending",
                Description = $"Hakediş: {a.JobListing.Title}"
            });
        }
        _db.JobLogs.Add(new JobLog { AssignedJobId = a.Id, CreatedBy = userId, Title = "İş tamamlandı", Description = "Acente onayladı." });
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        var subUserId = await _db.SubcontractorProfiles.Where(s => s.Id == a.SubcontractorId).Select(s => s.UserId).FirstAsync();
        _db.Notifications.Add(new Notification
        {
            UserId = subUserId, Type = "JOB_COMPLETED", Title = "İş Tamamlandı",
            Body = $"{a.JobListing.Title} onaylandı. Ödemeniz işleme alındı.",
            Data = System.Text.Json.JsonSerializer.Serialize(new { assignedJobId })
        });
        await _db.SaveChangesAsync();
        return MapAssignedJob(a);
    }

    public async Task<JobFileResponse> UploadJobFileAsync(Guid userId, Guid jobId, string fileName, string fileUrl, long? fileSize, string? fileType)
    {
        var agent = await GetAgentProfileAsync(userId);
        if (!await _db.JobListings.AnyAsync(j => j.Id == jobId && j.AgentId == agent.Id))
            throw new KeyNotFoundException("İlan bulunamadı.");
        var file = new JobFile { JobId = jobId, FileName = fileName, FileUrl = fileUrl, FileSize = (int?)fileSize, FileType = fileType, UploadedBy = userId };
        _db.JobFiles.Add(file);
        await _db.SaveChangesAsync();
        return new JobFileResponse { Id = file.Id, FileName = file.FileName, FileUrl = file.FileUrl, FileSize = file.FileSize, FileType = file.FileType, CreatedAt = file.CreatedAt };
    }

    // ─── PRIVATE ──────────────────────────────────────────────────────────────

    private async Task<AgentProfile> GetAgentProfileAsync(Guid userId)
        => await _db.AgentProfiles.FirstOrDefaultAsync(a => a.UserId == userId)
           ?? throw new UnauthorizedAccessException("Acente profili bulunamadı.");

    private static JobListingResponse MapJobListing(JobListing j) => new()
    {
        Id = j.Id, AgentId = j.AgentId, AgentCompanyName = j.Agent?.CompanyName ?? string.Empty,
        AgentLogoUrl = j.Agent?.LogoUrl, Title = j.Title, ListingType = j.ListingType,
        ShipName = j.ShipName, PortCode = j.PortCode, PortName = j.PortName, Location = j.Location,
        Category = j.Category, SelectedServices = j.SelectedServices?.ToList() ?? new(),
        Eta = j.Eta, NeedText = j.NeedText, BudgetMin = j.BudgetMin, BudgetMax = j.BudgetMax,
        Currency = j.Currency, Status = j.Status, OfferCount = j.OfferCount, Deadline = j.Deadline, CreatedAt = j.CreatedAt
    };

    private static AssignedJobResponse MapAssignedJob(AssignedJob a) => new()
    {
        Id = a.Id, JobId = a.JobId, JobTitle = a.JobListing?.Title ?? string.Empty,
        AgentCompanyName = a.Agent?.CompanyName ?? string.Empty, SubcontractorCompanyName = a.Subcontractor?.CompanyName ?? string.Empty,
        Progress = a.Progress, Status = a.Status, StartDate = a.StartDate, DueDate = a.DueDate,
        CompletedAt = a.CompletedAt, CreatedAt = a.CreatedAt
    };
}
