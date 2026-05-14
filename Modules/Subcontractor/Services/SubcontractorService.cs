using Microsoft.EntityFrameworkCore;
using Portlink.Api.Data;
using Portlink.Api.DTOs.Jobs;
using Portlink.Api.DTOs.Offers;
using Portlink.Api.Entities;
using Portlink.Api.Modules.Auth.Dtos;
using Portlink.Api.Modules.Common.Dtos;
using Portlink.Api.Modules.Subcontractor.Interfaces;

namespace Portlink.Api.Modules.Subcontractor;

public class SubcontractorService : ISubcontractorService
{
    private readonly AppDbContext _db;

    public SubcontractorService(AppDbContext db) => _db = db;

    // ─── PROFILE ─────────────────────────────────────────────────────────────

    public async Task<SubcontractorProfileResponse> GetSubcontractorProfileAsync(Guid userId)
    {
        var sub = await _db.SubcontractorProfiles
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == userId)
            ?? throw new UnauthorizedAccessException("Taşeron profili bulunamadı.");
        return MapProfileResponse(sub);
    }

    public async Task<SubcontractorProfileResponse> UpdateSubcontractorProfileAsync(Guid userId, UpdateSubcontractorProfileRequest req)
    {
        var sub = await _db.SubcontractorProfiles
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == userId)
            ?? throw new UnauthorizedAccessException("Taşeron profili bulunamadı.");

        if (req.CompanyName != null) sub.CompanyName = req.CompanyName.Trim();
        if (req.FullName != null) sub.FullName = req.FullName.Trim();
        if (req.Phone != null) sub.Phone = req.Phone.Trim();
        if (req.Country != null) sub.Country = req.Country.Trim();
        if (req.City != null) sub.City = req.City.Trim();
        if (req.ExpertiseTags != null) sub.ExpertiseTags = req.ExpertiseTags;
        sub.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return MapProfileResponse(sub);
    }

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
        var list = await _db.Offers
            .Include(o => o.JobListing)
                .ThenInclude(j => j.Agent)
            .Include(o => o.Subcontractor)
            .Where(o => o.SubcontractorId == sub.Id)
            .OrderByDescending(o => o.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return list.Select(MapOffer).ToList();
    }

    // ─── ACTIVE JOBS ─────────────────────────────────────────────────────────

    public async Task<List<AssignedJobResponse>> GetActiveJobsAsync(Guid userId, int page, int pageSize)
    {
        var sub = await GetProfileAsync(userId);
        var list = await _db.AssignedJobs.Include(a => a.JobListing).Include(a => a.Agent).Include(a => a.Subcontractor).Include(a => a.Offer)
            .Where(a => a.SubcontractorId == sub.Id && a.Status != "completed")
            .OrderByDescending(a => a.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return list.Select(MapAssignedJob).ToList();
    }

    public async Task<AssignedJobDetailResponse> GetActiveJobDetailAsync(Guid userId, Guid id)
    {
        var sub = await GetProfileAsync(userId);
        var a = await _db.AssignedJobs.Include(x => x.JobListing).Include(x => x.Agent).Include(x => x.Subcontractor).Include(x => x.Offer)
            .Include(x => x.JobLogs).Include(x => x.JobReports)
            .FirstOrDefaultAsync(x => x.Id == id && x.SubcontractorId == sub.Id)
            ?? throw new KeyNotFoundException("Atanmış iş bulunamadı.");
        return new AssignedJobDetailResponse
        {
            Id = a.Id,
            JobId = a.JobId,
            JobTitle = a.JobListing.Title,
            AgentUserId = a.Agent.UserId,
            AgentProfileId = a.AgentId,
            AgentLogoUrl = a.Agent.LogoUrl,
            AgentCompanyName = a.Agent.CompanyName,
            SubcontractorUserId = a.Subcontractor.UserId,
            SubcontractorProfileId = a.Subcontractor.Id,
            SubcontractorCompanyName = a.Subcontractor.CompanyName,
            Progress = a.Progress,
            Status = a.Status,
            StartDate = a.StartDate,
            DueDate = a.DueDate,
            CompletedAt = a.CompletedAt,
            CreatedAt = a.CreatedAt,
            OfferPrice = a.Offer?.Price ?? 0,
            OfferCurrency = a.Offer?.Currency ?? "TRY",
            Logs = a.JobLogs.OrderByDescending(l => l.CreatedAt).Select(MapJobLog).ToList(),
            Reports = a.JobReports.OrderByDescending(r => r.CreatedAt).Select(r => new JobReportResponse { Id = r.Id, FileName = r.FileName, FileUrl = r.FileUrl, FileSize = r.FileSize, FileType = r.FileType, CreatedAt = r.CreatedAt }).ToList()
        };
    }

    public async Task<AssignedJobResponse> UpdateActiveJobAsync(Guid userId, Guid id, UpdateAssignedJobRequest req)
    {
        var sub = await GetProfileAsync(userId);
        var a = await _db.AssignedJobs.Include(x => x.JobListing).Include(x => x.Agent).Include(x => x.Subcontractor).Include(x => x.Offer)
            .FirstOrDefaultAsync(x => x.Id == id && x.SubcontractorId == sub.Id)
            ?? throw new KeyNotFoundException("Atanmış iş bulunamadı.");
        if (req.Progress.HasValue) a.Progress = Math.Clamp(req.Progress.Value, 0, 90);
        if (req.Status != null)
        {
            if (req.Status == "completed")
                throw new InvalidOperationException("İşi tamamlamak için bitiş onayı gönderin.");
            a.Status = NormalizeSubcontractorStatus(req.Status);
            a.Progress = ProgressForStatus(a.Status, a.Progress);
        }
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

    public async Task<AssignedJobResponse> UpdateJobProgressAsync(Guid userId, Guid id, UpdateJobProgressRequest req)
    {
        var sub = await GetProfileAsync(userId);
        var a = await _db.AssignedJobs.Include(x => x.JobListing).Include(x => x.Agent).Include(x => x.Subcontractor).Include(x => x.Offer)
            .FirstOrDefaultAsync(x => x.Id == id && x.SubcontractorId == sub.Id)
            ?? throw new KeyNotFoundException("Atanmış iş bulunamadı.");

        if (a.Status == "completed")
            throw new InvalidOperationException("Tamamlanan iş güncellenemez.");

        var status = NormalizeSubcontractorStatus(req.Status);
        a.Status = status;
        a.Progress = ProgressForStatus(status, a.Progress);
        a.UpdatedAt = DateTime.UtcNow;

        _db.JobLogs.Add(new JobLog
        {
            AssignedJobId = a.Id,
            CreatedBy = userId,
            Type = "status",
            Title = StatusTitle(status),
            Description = req.Note?.Trim(),
            ReviewStatus = "none"
        });

        await _db.SaveChangesAsync();
        return MapAssignedJob(a);
    }

    public async Task<JobLogResponse> UploadPhotoLogAsync(Guid userId, Guid assignedJobId, string fileName, string fileUrl, long? fileSize, string? fileType, string? description)
    {
        var sub = await GetProfileAsync(userId);
        var assigned = await _db.AssignedJobs.Include(a => a.JobListing)
            .FirstOrDefaultAsync(a => a.Id == assignedJobId && a.SubcontractorId == sub.Id)
            ?? throw new KeyNotFoundException("Atanmış iş bulunamadı.");

        if (assigned.Status == "completed")
            throw new InvalidOperationException("Tamamlanan işe süreç logu eklenemez.");

        assigned.Status = "review";
        assigned.Progress = Math.Max(assigned.Progress, 80);
        assigned.UpdatedAt = DateTime.UtcNow;

        var log = new JobLog
        {
            AssignedJobId = assigned.Id,
            CreatedBy = userId,
            Type = "photo",
            Title = "Fotoğraflı süreç logu",
            Description = description?.Trim(),
            FileName = fileName,
            FileUrl = fileUrl,
            FileSize = fileSize,
            FileType = fileType,
            ReviewStatus = "pending"
        };
        _db.JobLogs.Add(log);

        var agentUserId = await _db.AgentProfiles.Where(a => a.Id == assigned.AgentId).Select(a => a.UserId).FirstAsync();
        _db.Notifications.Add(new Notification
        {
            UserId = agentUserId,
            Type = "JOB_LOG_PENDING_REVIEW",
            Title = "Süreç Logu Onay Bekliyor",
            Body = $"{assigned.JobListing.Title} için taşeron fotoğraflı süreç logu yükledi.",
            Data = System.Text.Json.JsonSerializer.Serialize(new { assignedJobId, logId = log.Id })
        });

        await _db.SaveChangesAsync();
        return MapJobLog(log);
    }

    public async Task<AssignedJobResponse> SubmitJobForCompletionAsync(Guid userId, Guid assignedJobId, string? note)
    {
        var sub = await GetProfileAsync(userId);
        var assigned = await _db.AssignedJobs.Include(a => a.JobListing).Include(a => a.Agent).Include(a => a.Subcontractor).Include(a => a.Offer)
            .FirstOrDefaultAsync(a => a.Id == assignedJobId && a.SubcontractorId == sub.Id)
            ?? throw new KeyNotFoundException("Atanmış iş bulunamadı.");

        if (assigned.Status == "completed")
            throw new InvalidOperationException("İş zaten tamamlandı.");

        assigned.Status = "review";
        assigned.Progress = Math.Max(assigned.Progress, 90);
        assigned.UpdatedAt = DateTime.UtcNow;

        _db.JobLogs.Add(new JobLog
        {
            AssignedJobId = assigned.Id,
            CreatedBy = userId,
            Type = "status",
            Title = "İş bitiş onayına gönderildi",
            Description = note?.Trim(),
            ReviewStatus = "pending"
        });

        var agentUserId = await _db.AgentProfiles.Where(a => a.Id == assigned.AgentId).Select(a => a.UserId).FirstAsync();
        _db.Notifications.Add(new Notification
        {
            UserId = agentUserId,
            Type = "JOB_COMPLETION_REQUESTED",
            Title = "İş Bitiş Onayı Bekliyor",
            Body = $"{assigned.JobListing.Title} için taşeron işi bitirdiğini bildirdi.",
            Data = System.Text.Json.JsonSerializer.Serialize(new { assignedJobId })
        });

        await _db.SaveChangesAsync();
        return MapAssignedJob(assigned);
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

    // ─── LOGO ─────────────────────────────────────────────────────────────────

    public async Task<string> UploadLogoAsync(Guid userId, string logoUrl)
    {
        var sub = await _db.SubcontractorProfiles.FirstOrDefaultAsync(s => s.UserId == userId)
            ?? throw new UnauthorizedAccessException("Taşeron profili bulunamadı.");
        sub.LogoUrl = logoUrl;
        sub.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return logoUrl;
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

    private static SubcontractorProfileResponse MapProfileResponse(SubcontractorProfile sub) => new()
    {
        Id = sub.Id,
        FullName = sub.FullName,
        CompanyName = sub.CompanyName,
        Email = sub.User?.Email,
        Phone = sub.Phone,
        Country = sub.Country,
        City = sub.City,
        LogoUrl = sub.LogoUrl,
        Rating = sub.Rating,
        RatingCount = sub.RatingCount,
        TotalCompleted = sub.TotalCompleted,
        ExpertiseTags = sub.ExpertiseTags,
        IsVerified = sub.IsVerified
    };

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
        AgentUserId = o.JobListing?.Agent?.UserId ?? Guid.Empty,
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
        AgentUserId = a.Agent?.UserId ?? Guid.Empty,
        AgentProfileId = a.AgentId,
        AgentLogoUrl = a.Agent?.LogoUrl,
        SubcontractorUserId = a.Subcontractor?.UserId ?? Guid.Empty,
        SubcontractorProfileId = a.Subcontractor?.Id ?? Guid.Empty,
        AgentCompanyName = a.Agent?.CompanyName ?? string.Empty,
        SubcontractorCompanyName = a.Subcontractor?.CompanyName ?? string.Empty,
        Progress = a.Progress,
        Status = a.Status,
        StartDate = a.StartDate,
        DueDate = a.DueDate,
        CompletedAt = a.CompletedAt,
        CreatedAt = a.CreatedAt,
        OfferPrice = a.Offer?.Price ?? 0,
        OfferCurrency = a.Offer?.Currency ?? "TRY"
    };

    private static JobLogResponse MapJobLog(JobLog l) => new()
    {
        Id = l.Id,
        Title = l.Title,
        Description = l.Description,
        Type = l.Type,
        FileUrl = l.FileUrl,
        FileName = l.FileName,
        FileType = l.FileType,
        FileSize = l.FileSize,
        ReviewStatus = l.ReviewStatus,
        CreatedBy = l.CreatedBy,
        ReviewedBy = l.ReviewedBy,
        ReviewedAt = l.ReviewedAt,
        ReviewNote = l.ReviewNote,
        CreatedAt = l.CreatedAt
    };

    private static string NormalizeSubcontractorStatus(string status)
    {
        var normalized = status.Trim().ToLowerInvariant();
        return normalized switch
        {
            "started" or "basladi" or "başladı" => "started",
            "in_progress" or "devam" or "devam_ediyor" or "devam ediyor" => "in_progress",
            "review" => "review",
            "completed" or "bitti" => throw new InvalidOperationException("Bitti durumu acente onayı gerektirir."),
            _ => throw new InvalidOperationException("Geçersiz iş durumu.")
        };
    }

    private static int ProgressForStatus(string status, int currentProgress) => status switch
    {
        "started" => Math.Max(currentProgress, 25),
        "in_progress" => Math.Max(currentProgress, 60),
        "review" => Math.Max(currentProgress, 80),
        _ => currentProgress
    };

    private static string StatusTitle(string status) => status switch
    {
        "started" => "İş başladı",
        "in_progress" => "İş devam ediyor",
        "review" => "Acente onayı bekleniyor",
        _ => "İş durumu güncellendi"
    };

    public async Task<AgentProfileResponse> GetAgentPublicProfileAsync(Guid userId, Guid agentProfileId)
    {
        var agent = await _db.AgentProfiles
            .Include(a => a.User)
            .Include(a => a.Ports)
            .FirstOrDefaultAsync(a => a.Id == agentProfileId)
            ?? throw new KeyNotFoundException("Acente profili bulunamadı.");

        var hasRated = await _db.Ratings
            .AnyAsync(r => r.RaterUserId == userId && r.RateeProfileId == agentProfileId);

        var breakdownRaw = await _db.Ratings
            .Where(r => r.RateeProfileId == agentProfileId)
            .GroupBy(r => (int)Math.Round((double)r.Score))
            .Select(g => new { Star = g.Key, Count = g.Count() })
            .ToListAsync();

        var breakdown = new Dictionary<int, int> { {1,0},{2,0},{3,0},{4,0},{5,0} };
        foreach (var b in breakdownRaw)
            if (b.Star >= 1 && b.Star <= 5) breakdown[b.Star] = b.Count;

        return new AgentProfileResponse
        {
            Id = agent.Id,
            Email = agent.User?.Email ?? string.Empty,
            FullName = agent.FullName,
            CompanyName = agent.CompanyName,
            Phone = agent.Phone,
            Bio = agent.Bio,
            Country = agent.Country,
            City = agent.City,
            LogoUrl = agent.LogoUrl,
            Rating = agent.Rating,
            RatingCount = agent.RatingCount,
            TotalJobs = agent.TotalJobs,
            IsVerified = agent.IsVerified,
            HasCurrentUserRated = hasRated,
            RatingBreakdown = breakdown,
            Ports = agent.Ports.Select(p => new PortResponse
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Region = p.Region,
                Coordinates = p.Coordinates
            }).ToList()
        };
    }

    public async Task RateAgentAsync(Guid userId, Guid agentProfileId, decimal rating)
    {
        var agent = await _db.AgentProfiles.FirstOrDefaultAsync(a => a.Id == agentProfileId)
            ?? throw new KeyNotFoundException("Acente profili bulunamadı.");

        var alreadyRated = await _db.Ratings
            .AnyAsync(r => r.RaterUserId == userId && r.RateeProfileId == agentProfileId);
        if (alreadyRated)
            throw new InvalidOperationException("Bu acenteyi daha önce puanladınız.");

        agent.Rating = (agent.Rating * agent.RatingCount + rating) / (agent.RatingCount + 1);
        agent.RatingCount++;

        _db.Ratings.Add(new Portlink.Api.Entities.Rating
        {
            RaterUserId = userId,
            RateeProfileId = agentProfileId,
            Score = rating
        });

        await _db.SaveChangesAsync();
    }
}
