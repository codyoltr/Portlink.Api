using Microsoft.EntityFrameworkCore;
using Portlink.Api.Data;
using Portlink.Api.DTOs.Jobs;
using Portlink.Api.DTOs.Offers;
using Portlink.Api.Entities;
using Portlink.Api.Modules.Common.Dtos;

namespace Portlink.Api.Modules.Subcontractor;

public class SubcontractorService
{
    private readonly AppDbContext _db;

    public SubcontractorService(AppDbContext db) => _db = db;

    // ─── DASHBOARD ───────────────────────────────────────────────────────────

    public async Task<SubcontractorDashboardStatsResponse> GetDashboardStatsAsync(Guid userId)
    {
        var sub = await GetProfileAsync(userId);
        var totalEarnings = await _db.WalletTransactions.Where(w => w.SubcontractorId == sub.Id && w.Type == "earning" && w.Status == "completed").SumAsync(w => (decimal?)w.Amount) ?? 0;
        var pendingEarnings = await _db.WalletTransactions.Where(w => w.SubcontractorId == sub.Id && w.Type == "earning" && w.Status == "pending").SumAsync(w => (decimal?)w.Amount) ?? 0;
        return new SubcontractorDashboardStatsResponse
        {
            ActiveBids = await _db.Offers.CountAsync(o => o.SubcontractorId == sub.Id && o.Status == "pending"),
            AcceptedBids = await _db.Offers.CountAsync(o => o.SubcontractorId == sub.Id && o.Status == "accepted"),
            ActiveJobs = await _db.AssignedJobs.CountAsync(a => a.SubcontractorId == sub.Id && a.Status != "completed"),
            CompletedJobs = await _db.AssignedJobs.CountAsync(a => a.SubcontractorId == sub.Id && a.Status == "completed"),
            TotalEarnings = totalEarnings,
            PendingEarnings = pendingEarnings
        };
    }

    // ─── MARKET (PUBLIC JOB LISTINGS) ────────────────────────────────────────

    public async Task<List<JobListingResponse>> ListActiveJobsAsync(string? category, string? location, string? search, int page, int pageSize)
    {
        var query = _db.JobListings.Include(j => j.Agent).Where(j => j.Status == "active");
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(j => j.Category == category);
        if (!string.IsNullOrWhiteSpace(location)) query = query.Where(j => j.Location != null && j.Location.Contains(location));
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(j => j.Title.Contains(search) || (j.NeedText != null && j.NeedText.Contains(search)));
        var list = await query.OrderByDescending(j => j.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return list.Select(MapJobListing).ToList();
    }

    public async Task<JobListingDetailResponse> GetJobDetailAsync(Guid jobId)
    {
        var job = await _db.JobListings.Include(j => j.Agent).Include(j => j.JobFiles)
            .FirstOrDefaultAsync(j => j.Id == jobId && j.Status == "active")
            ?? throw new KeyNotFoundException("İlan bulunamadı.");
        return new JobListingDetailResponse
        {
            Id = job.Id,
            AgentId = job.AgentId,
            AgentCompanyName = job.Agent.CompanyName,
            AgentLogoUrl = job.Agent.LogoUrl,
            Title = job.Title,
            ListingType = job.ListingType,
            ShipName = job.ShipName,
            PortCode = job.PortCode,
            PortName = job.PortName,
            Location = job.Location,
            Category = job.Category,
            SelectedServices = job.SelectedServices?.ToList() ?? new(),
            Eta = job.Eta,
            NeedText = job.NeedText,
            BudgetMin = job.BudgetMin,
            BudgetMax = job.BudgetMax,
            Currency = job.Currency,
            Status = job.Status,
            OfferCount = job.OfferCount,
            Deadline = job.Deadline,
            CreatedAt = job.CreatedAt,
            Files = job.JobFiles.Select(f => new JobFileResponse { Id = f.Id, FileName = f.FileName, FileUrl = f.FileUrl, FileSize = f.FileSize, FileType = f.FileType, CreatedAt = f.CreatedAt }).ToList()
        };
    }

    // ─── OFFERS ──────────────────────────────────────────────────────────────

    public async Task<OfferResponse> CreateOfferAsync(Guid userId, Guid jobId, CreateOfferRequest req)
    {
        var sub = await GetProfileAsync(userId);
        var job = await _db.JobListings.Include(j => j.Agent).FirstOrDefaultAsync(j => j.Id == jobId && j.Status == "active")
            ?? throw new KeyNotFoundException("İlan bulunamadı veya artık aktif değil.");
        if (await _db.Offers.AnyAsync(o => o.JobId == jobId && o.SubcontractorId == sub.Id))
            throw new InvalidOperationException("Bu ilana zaten teklif verdiniz.");

        var offer = new Offer { JobId = jobId, SubcontractorId = sub.Id, Price = req.Price, Currency = req.Currency, EstimatedDays = req.EstimatedDays, CoverNote = req.CoverNote?.Trim(), Status = "pending" };
        _db.Offers.Add(offer);
        job.OfferCount++;

        // Acenteye bildirim
        var agentUserId = await _db.AgentProfiles.Where(a => a.Id == job.AgentId).Select(a => a.UserId).FirstAsync();
        _db.Notifications.Add(new Notification
        {
            UserId = agentUserId,
            Type = "NEW_OFFER",
            Title = "Yeni Teklif",
            Body = $"{sub.CompanyName} firması '{job.Title}' ilanına teklif verdi.",
            Data = System.Text.Json.JsonSerializer.Serialize(new { jobId, offerId = offer.Id })
        });
        await _db.SaveChangesAsync();

        await _db.Entry(offer).Reference(o => o.JobListing).LoadAsync();
        await _db.Entry(offer).Reference(o => o.Subcontractor).LoadAsync();
        return MapOffer(offer);
    }

    public async Task WithdrawOfferAsync(Guid userId, Guid offerId)
    {
        var sub = await GetProfileAsync(userId);
        var offer = await _db.Offers.FirstOrDefaultAsync(o => o.Id == offerId && o.SubcontractorId == sub.Id)
            ?? throw new KeyNotFoundException("Teklif bulunamadı.");
        if (offer.Status != "pending") throw new InvalidOperationException("Yalnızca bekleyen teklifler geri çekilebilir.");
        offer.Status = "withdrawn"; offer.UpdatedAt = DateTime.UtcNow;

        // OfferCount düşür
        var job = await _db.JobListings.FindAsync(offer.JobId);
        if (job != null && job.OfferCount > 0) job.OfferCount--;
        await _db.SaveChangesAsync();
    }

    public async Task<List<OfferResponse>> GetMyOffersAsync(Guid userId, int page, int pageSize)
    {
        var sub = await GetProfileAsync(userId);
        var list = await _db.Offers.Include(o => o.JobListing).Include(o => o.Subcontractor)
            .Where(o => o.SubcontractorId == sub.Id)
            .OrderByDescending(o => o.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return list.Select(MapOffer).ToList();
    }

    // ─── ACTIVE JOBS ─────────────────────────────────────────────────────────

    public async Task<List<AssignedJobResponse>> GetActiveJobsAsync(Guid userId, int page, int pageSize)
    {
        var sub = await GetProfileAsync(userId);
        var list = await _db.AssignedJobs.Include(a => a.JobListing).Include(a => a.Agent).Include(a => a.Subcontractor)
            .Where(a => a.SubcontractorId == sub.Id)
            .OrderByDescending(a => a.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return list.Select(MapAssignedJob).ToList();
    }

    public async Task<AssignedJobDetailResponse> GetActiveJobDetailAsync(Guid userId, Guid id)
    {
        var sub = await GetProfileAsync(userId);
        var a = await _db.AssignedJobs.Include(x => x.JobListing).Include(x => x.Agent).Include(x => x.Subcontractor)
            .Include(x => x.JobLogs).Include(x => x.JobReports)
            .FirstOrDefaultAsync(x => x.Id == id && x.SubcontractorId == sub.Id)
            ?? throw new KeyNotFoundException("Atanmış iş bulunamadı.");
        return new AssignedJobDetailResponse
        {
            Id = a.Id,
            JobId = a.JobId,
            JobTitle = a.JobListing.Title,
            AgentCompanyName = a.Agent.CompanyName,
            SubcontractorCompanyName = a.Subcontractor.CompanyName,
            Progress = a.Progress,
            Status = a.Status,
            StartDate = a.StartDate,
            DueDate = a.DueDate,
            CompletedAt = a.CompletedAt,
            CreatedAt = a.CreatedAt,
            Logs = a.JobLogs.OrderByDescending(l => l.CreatedAt).Select(l => new JobLogResponse { Id = l.Id, Title = l.Title, Description = l.Description, CreatedAt = l.CreatedAt }).ToList(),
            Reports = a.JobReports.OrderByDescending(r => r.CreatedAt).Select(r => new JobReportResponse { Id = r.Id, FileName = r.FileName, FileUrl = r.FileUrl, FileSize = r.FileSize, FileType = r.FileType, CreatedAt = r.CreatedAt }).ToList()
        };
    }

    public async Task<AssignedJobResponse> UpdateActiveJobAsync(Guid userId, Guid id, UpdateAssignedJobRequest req)
    {
        var sub = await GetProfileAsync(userId);
        var a = await _db.AssignedJobs.Include(x => x.JobListing).Include(x => x.Agent).Include(x => x.Subcontractor)
            .FirstOrDefaultAsync(x => x.Id == id && x.SubcontractorId == sub.Id)
            ?? throw new KeyNotFoundException("Atanmış iş bulunamadı.");
        if (req.Progress.HasValue) a.Progress = Math.Clamp(req.Progress.Value, 0, 100);
        if (req.Status != null) a.Status = req.Status;
        if (req.StartDate.HasValue) a.StartDate = req.StartDate;
        if (req.DueDate.HasValue) a.DueDate = req.DueDate;
        a.UpdatedAt = DateTime.UtcNow;

        // Log ekle
        if (req.Progress.HasValue)
        {
            _db.JobLogs.Add(new JobLog { AssignedJobId = a.Id, CreatedBy = userId, Title = $"İlerleme güncellendi: %{req.Progress.Value}" });
        }
        await _db.SaveChangesAsync();
        return MapAssignedJob(a);
    }

    public async Task<JobReportResponse> UploadReportAsync(Guid userId, Guid assignedJobId, string fileName, string fileUrl, long? fileSize, string? fileType)
    {
        var sub = await GetProfileAsync(userId);
        var assigned = await _db.AssignedJobs.Include(a => a.JobListing)
            .FirstOrDefaultAsync(a => a.Id == assignedJobId && a.SubcontractorId == sub.Id)
            ?? throw new KeyNotFoundException("Atanmış iş bulunamadı.");

        var report = new JobReport { AssignedJobId = assigned.Id, UploadedBy = userId, FileName = fileName, FileUrl = fileUrl, FileSize = (int?)fileSize, FileType = fileType };
        _db.JobReports.Add(report);

        // Acenteye bildirim
        var agentUserId = await _db.AgentProfiles.Where(a => a.Id == assigned.AgentId).Select(a => a.UserId).FirstAsync();
        _db.Notifications.Add(new Notification
        {
            UserId = agentUserId,
            Type = "NEW_REPORT",
            Title = "Yeni Rapor Yüklendi",
            Body = $"{assigned.JobListing.Title} için yeni rapor yüklendi.",
            Data = System.Text.Json.JsonSerializer.Serialize(new { assignedJobId, reportId = report.Id })
        });
        await _db.SaveChangesAsync();
        return new JobReportResponse { Id = report.Id, FileName = report.FileName, FileUrl = report.FileUrl, FileSize = report.FileSize, FileType = report.FileType, CreatedAt = report.CreatedAt };
    }

    // ─── WALLET ───────────────────────────────────────────────────────────────

    public async Task<WalletResponse> GetWalletAsync(Guid userId)
    {
        var sub = await GetProfileAsync(userId);
        var txs = await _db.WalletTransactions.Where(w => w.SubcontractorId == sub.Id).OrderByDescending(w => w.CreatedAt).ToListAsync();
        return new WalletResponse
        {
            TotalEarnings = txs.Where(t => t.Status == "completed").Sum(t => t.Amount),
            PendingEarnings = txs.Where(t => t.Status == "pending").Sum(t => t.Amount),
            CompletedEarnings = txs.Where(t => t.Status == "completed").Sum(t => t.Amount),
            Transactions = txs.Select(t => new WalletTransactionResponse
            {
                Id = t.Id,
                Type = t.Type,
                Amount = t.Amount,
                Currency = t.Currency,
                Status = t.Status,
                Description = t.Description,
                CreatedAt = t.CreatedAt
            }).ToList()
        };
    }

    // ─── PRIVATE ─────────────────────────────────────────────────────────────

    private async Task<SubcontractorProfile> GetProfileAsync(Guid userId)
        => await _db.SubcontractorProfiles.FirstOrDefaultAsync(s => s.UserId == userId)
           ?? throw new UnauthorizedAccessException("Taşeron profili bulunamadı.");

    private static JobListingResponse MapJobListing(JobListing j) => new()
    {
        Id = j.Id,
        AgentId = j.AgentId,
        AgentCompanyName = j.Agent?.CompanyName ?? string.Empty,
        AgentLogoUrl = j.Agent?.LogoUrl,
        Title = j.Title,
        ListingType = j.ListingType,
        ShipName = j.ShipName,
        PortCode = j.PortCode,
        PortName = j.PortName,
        Location = j.Location,
        Category = j.Category,
        SelectedServices = j.SelectedServices?.ToList() ?? new(),
        Eta = j.Eta,
        NeedText = j.NeedText,
        BudgetMin = j.BudgetMin,
        BudgetMax = j.BudgetMax,
        Currency = j.Currency,
        Status = j.Status,
        OfferCount = j.OfferCount,
        Deadline = j.Deadline,
        CreatedAt = j.CreatedAt
    };

    private static OfferResponse MapOffer(Offer o) => new()
    {
        Id = o.Id,
        JobId = o.JobId,
        JobTitle = o.JobListing?.Title ?? string.Empty,
        SubcontractorId = o.SubcontractorId,
        SubcontractorCompanyName = o.Subcontractor?.CompanyName ?? string.Empty,
        SubcontractorLogoUrl = o.Subcontractor?.LogoUrl,
        SubcontractorRating = o.Subcontractor?.Rating ?? 0,
        Price = o.Price,
        Currency = o.Currency,
        EstimatedDays = o.EstimatedDays,
        CoverNote = o.CoverNote,
        Status = o.Status,
        CreatedAt = o.CreatedAt
    };

    private static AssignedJobResponse MapAssignedJob(AssignedJob a) => new()
    {
        Id = a.Id,
        JobId = a.JobId,
        JobTitle = a.JobListing?.Title ?? string.Empty,
        AgentCompanyName = a.Agent?.CompanyName ?? string.Empty,
        SubcontractorCompanyName = a.Subcontractor?.CompanyName ?? string.Empty,
        Progress = a.Progress,
        Status = a.Status,
        StartDate = a.StartDate,
        DueDate = a.DueDate,
        CompletedAt = a.CompletedAt,
        CreatedAt = a.CreatedAt
    };
}
