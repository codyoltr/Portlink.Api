using Microsoft.EntityFrameworkCore;
using Portlink.Api.Data;
using Portlink.Api.DTOs.Jobs;
using Portlink.Api.DTOs.Offers;
using Portlink.Api.Entities;
using Portlink.Api.DTOs.Agents;
using Portlink.Api.Modules.Auth.Dtos;
using Portlink.Api.Modules.Auth.Entities;
using Portlink.Api.Modules.Common.Dtos;

namespace Portlink.Api.Modules.Agent;

public class AgentService : IAgentService
{
    private readonly AppDbContext _db;

    public AgentService(AppDbContext db)
    {
        _db = db;
    }

    // ─── PROFILE ─────────────────────────────────────────────────────────────

    public async Task<AgentProfileResponse> GetProfileAsync(Guid userId)
    {
        var agent = await _db.AgentProfiles.Include(a => a.User).Include(a => a.Ports).FirstOrDefaultAsync(a => a.UserId == userId)
            ?? throw new UnauthorizedAccessException("Acente profili bulunamadı.");
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
            TotalJobs = agent.TotalJobs,
            IsVerified = agent.IsVerified,
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

    public async Task<AgentProfileResponse> UpdateProfileAsync(Guid userId, UpdateAgencyProfileRequest req)
    {
        var agent = await _db.AgentProfiles.Include(a => a.User).Include(a => a.Ports).FirstOrDefaultAsync(a => a.UserId == userId)
            ?? throw new UnauthorizedAccessException("Acente profili bulunamadı.");

        if (req.FullName != null) agent.FullName = req.FullName.Trim();
        if (req.CompanyName != null) agent.CompanyName = req.CompanyName.Trim();
        if (req.Phone != null) agent.Phone = req.Phone.Trim();
        if (req.Bio != null) agent.Bio = req.Bio.Trim();
        if (req.Country != null) agent.Country = req.Country.Trim();
        if (req.City != null) agent.City = req.City.Trim();

        if (req.Email != null && agent.User != null)
        {
            var email = req.Email.Trim();
            if (agent.User.Email != email)
            {
                var exists = await _db.Users.AnyAsync(u => u.Email == email && u.Id != userId);
                if (exists) throw new InvalidOperationException("Bu e-posta adresi kullanımda.");
                agent.User.Email = email;
            }
        }

        if (req.PortIds != null)
        {
            var ports = await _db.Ports.Where(p => req.PortIds.Contains(p.Id)).ToListAsync();
            agent.Ports.Clear();
            foreach (var p in ports) agent.Ports.Add(p);
        }

        agent.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

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
            TotalJobs = agent.TotalJobs,
            IsVerified = agent.IsVerified,
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

    public async Task<string> UploadLogoAsync(Guid userId, string logoUrl)
    {
        var agent = await _db.AgentProfiles.FirstOrDefaultAsync(a => a.UserId == userId)
            ?? throw new UnauthorizedAccessException("Acente profili bulunamadı.");
        agent.LogoUrl = logoUrl;
        agent.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return logoUrl;
    }

    // ─── DASHBOARD ───────────────────────────────────────────────────────────

    public async Task<AgentDashboardStatsResponse> GetDashboardStatsAsync(Guid userId)
    {
        var agent = await GetAgentProfileAsync(userId);
        return new AgentDashboardStatsResponse
        {
            ActiveListings = await _db.JobListings.CountAsync(j => j.AgentId == agent.Id && j.Status == "active"),
            TotalOffers = await _db.Offers.CountAsync(o => o.JobListing.AgentId == agent.Id && o.Status == "pending"),
            ActiveJobs = await _db.AssignedJobs.CountAsync(a => a.AgentId == agent.Id && a.Status != "completed"),
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

    public async Task<List<JobListingResponse>> ListMarketplaceJobsAsync(string? category, string? location, string? search, int page, int pageSize)
    {
        var query = _db.JobListings.Include(j => j.Agent).Where(j => j.Status == "active");
        
        if (!string.IsNullOrWhiteSpace(category) && category != "Tümü") 
            query = query.Where(j => j.Category == category);
            
        if (!string.IsNullOrWhiteSpace(location) && location != "Tüm Limanlar") 
            query = query.Where(j => j.Location != null && j.Location.Contains(location));
            
        if (!string.IsNullOrWhiteSpace(search)) 
            query = query.Where(j => j.Title.Contains(search) || (j.NeedText != null && j.NeedText.Contains(search)));
            
        var list = await query.OrderByDescending(j => j.CreatedAt)
                             .Skip((page - 1) * pageSize)
                             .Take(pageSize)
                             .ToListAsync();
                             
        return list.Select(MapJobListing).ToList();
    }

    public async Task<JobListingDetailResponse> GetJobDetailAsync(Guid userId, Guid jobId)
    {
        var agent = await GetAgentProfileAsync(userId);
        var job = await _db.JobListings.Include(j => j.Agent).Include(j => j.JobFiles)
            .FirstOrDefaultAsync(j => j.Id == jobId && j.AgentId == agent.Id)
            ?? throw new KeyNotFoundException("İlan bulunamadı.");
        var r = new JobListingDetailResponse
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
            Files = job.JobFiles.Select(f => new JobFileResponse
            {
                Id = f.Id,
                FileName = f.FileName,
                FileUrl = f.FileUrl,
                FileSize = f.FileSize,
                FileType = f.FileType,
                CreatedAt = f.CreatedAt
            }).ToList()
        };
        return r;
    }

    public async Task<JobListingResponse> CreateJobAsync(Guid userId, CreateJobListingRequest req)
    {
        var agent = await GetAgentProfileAsync(userId);
        var job = new JobListing
        {
            AgentId = agent.Id,
            Title = req.Title.Trim(),
            ListingType = req.ListingType,
            ShipName = req.ShipName?.Trim(),
            PortCode = req.PortCode?.Trim(),
            PortName = req.PortName?.Trim(),
            Location = req.Location?.Trim(),
            Category = req.Category.Trim(),
            SelectedServices = req.SelectedServices ?? new List<string>(),
            Eta = req.Eta,
            NeedText = req.NeedText?.Trim(),
            BudgetMin = req.BudgetMin,
            BudgetMax = req.BudgetMax,
            Currency = req.Currency,
            Deadline = req.Deadline,
            Status = "active"
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
                Id = o.Id,
                JobId = o.JobId,
                JobTitle = o.JobListing.Title,
                AgentUserId = o.JobListing.Agent.UserId,
                SubcontractorId = o.SubcontractorId,
                SubcontractorCompanyName = o.Subcontractor.CompanyName,
                SubcontractorLogoUrl = o.Subcontractor.LogoUrl,
                SubcontractorRating = o.Subcontractor.Rating,
                Price = o.Price,
                Currency = o.Currency,
                EstimatedDays = o.EstimatedDays,
                CoverNote = o.CoverNote,
                Status = o.Status,
                CreatedAt = o.CreatedAt
            }).ToListAsync();
    }

    public async Task<AgentOffersDashboardResponse> GetAllOffersAsync(Guid userId, AgentOffersQueryRequest request)
    {
        var agent = await GetAgentProfileAsync(userId);
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : request.PageSize > 100 ? 100 : request.PageSize;

        var query = _db.Offers
            .AsNoTracking()
            .Where(o => o.JobListing.AgentId == agent.Id);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(o => o.Status == request.Status);
        }

        if (request.JobListingId.HasValue)
        {
            query = query.Where(o => o.JobId == request.JobListingId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Location))
        {
            var location = request.Location.Trim().ToLower();
            query = query.Where(o =>
                (o.JobListing.Location != null && o.JobListing.Location.ToLower().Contains(location)) ||
                (o.JobListing.PortName != null && o.JobListing.PortName.ToLower().Contains(location)));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(o =>
                o.JobListing.Title.ToLower().Contains(search) ||
                (o.JobListing.ShipName != null && o.JobListing.ShipName.ToLower().Contains(search)) ||
                o.Subcontractor.CompanyName.ToLower().Contains(search) ||
                o.Subcontractor.FullName.ToLower().Contains(search) ||
                (o.CoverNote != null && o.CoverNote.ToLower().Contains(search)));
        }

        query = ApplyOfferSorting(query, request.SortBy, request.SortDirection);

        var totalOffers = await _db.Offers.AsNoTracking().CountAsync(o => o.JobListing.AgentId == agent.Id);
        var pendingOffers = await _db.Offers.AsNoTracking().CountAsync(o => o.JobListing.AgentId == agent.Id && o.Status == "pending");
        var acceptedOffers = await _db.Offers.AsNoTracking().CountAsync(o => o.JobListing.AgentId == agent.Id && o.Status == "accepted");
        var averageOfferAmount = await _db.Offers
            .AsNoTracking()
            .Where(o => o.JobListing.AgentId == agent.Id)
            .AverageAsync(o => (decimal?)o.Price);

        var totalCount = await query.CountAsync();
        var offers = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new AgentOfferListItemResponse
            {
                Id = o.Id,
                JobId = o.JobId,
                JobTitle = o.JobListing.Title,
                ShipName = o.JobListing.ShipName,
                AgentCompanyName = o.JobListing.Agent.CompanyName,
                Location = o.JobListing.Location ?? o.JobListing.PortName,
                Price = o.Price,
                Currency = o.Currency,
                EstimatedDays = o.EstimatedDays,
                CoverNote = o.CoverNote,
                Status = o.Status,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt,
                SubcontractorId = o.SubcontractorId,
                SubcontractorCompanyName = o.Subcontractor.CompanyName,
                SubcontractorFullName = o.Subcontractor.FullName,
                SubcontractorLogoUrl = o.Subcontractor.LogoUrl,
                SubcontractorRating = o.Subcontractor.Rating,
                SubcontractorCompletedJobsCount = o.Subcontractor.TotalCompleted,
                SubcontractorIsVerified = o.Subcontractor.IsVerified
            })
            .ToListAsync();

        return new AgentOffersDashboardResponse
        {
            Offers = new PaginatedResponse<AgentOfferListItemResponse>
            {
                Items = offers,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            },
            TotalOffers = totalOffers,
            PendingOffers = pendingOffers,
            AcceptedOffers = acceptedOffers,
            AverageOfferAmount = averageOfferAmount
        };
    }

    public async Task<AgentOfferDetailResponse> GetOfferDetailAsync(Guid userId, Guid offerId)
    {
        var agent = await GetAgentProfileAsync(userId);

        return await _db.Offers
            .AsNoTracking()
            .Where(o => o.Id == offerId && o.JobListing.AgentId == agent.Id)
            .Select(o => new AgentOfferDetailResponse
            {
                Id = o.Id,
                JobId = o.JobId,
                JobTitle = o.JobListing.Title,
                ShipName = o.JobListing.ShipName,
                AgentCompanyName = o.JobListing.Agent.CompanyName,
                Location = o.JobListing.Location ?? o.JobListing.PortName,
                Price = o.Price,
                Currency = o.Currency,
                EstimatedDays = o.EstimatedDays,
                CoverNote = o.CoverNote,
                Status = o.Status,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt,
                SubcontractorId = o.SubcontractorId,
                SubcontractorCompanyName = o.Subcontractor.CompanyName,
                SubcontractorFullName = o.Subcontractor.FullName,
                SubcontractorLogoUrl = o.Subcontractor.LogoUrl,
                SubcontractorRating = o.Subcontractor.Rating,
                SubcontractorCompletedJobsCount = o.Subcontractor.TotalCompleted,
                SubcontractorIsVerified = o.Subcontractor.IsVerified,
                PortName = o.JobListing.PortName,
                PortCode = o.JobListing.PortCode,
                NeedText = o.JobListing.NeedText,
                JobStatus = o.JobListing.Status,
                Deadline = o.JobListing.Deadline
            })
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Teklif bulunamadı.");
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
            JobId = offer.JobId,
            OfferId = offer.Id,
            AgentId = agent.Id,
            SubcontractorId = offer.SubcontractorId,
            Status = "planning",
            Progress = 0
        };
        _db.AssignedJobs.Add(assigned);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        _db.JobLogs.Add(new JobLog
        {
            AssignedJobId = assigned.Id,
            CreatedBy = userId,
            Title = "İş başlatıldı",
            Description = "Teklif kabul edildi, iş planlanıyor."
        });

        // Taşeronun user'ına bildirim
        var subUserId = await _db.SubcontractorProfiles.Where(s => s.Id == offer.SubcontractorId).Select(s => s.UserId).FirstAsync();
        _db.Notifications.Add(new Notification
        {
            UserId = subUserId,
            Type = "OFFER_ACCEPTED",
            Title = "Teklifiniz Kabul Edildi",
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
        var query = _db.AssignedJobs.Include(a => a.JobListing).Include(a => a.Agent).Include(a => a.Subcontractor).Include(a => a.Offer)
            .Where(a => a.AgentId == agent.Id);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(a => a.Status == status);
        var list = await query.OrderByDescending(a => a.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return list.Select(MapAssignedJob).ToList();
    }

    public async Task<AssignedJobDetailResponse> GetAssignedJobDetailAsync(Guid userId, Guid id)
    {
        var agent = await GetAgentProfileAsync(userId);
        var a = await _db.AssignedJobs
            .Include(x => x.JobListing).Include(x => x.Agent).Include(x => x.Subcontractor).Include(x => x.Offer)
            .Include(x => x.JobLogs).Include(x => x.JobReports)
            .FirstOrDefaultAsync(x => x.Id == id && x.AgentId == agent.Id)
            ?? throw new KeyNotFoundException("Atanmış iş bulunamadı.");
        return new AssignedJobDetailResponse
        {
            Id = a.Id,
            JobId = a.JobId,
            JobTitle = a.JobListing.Title,
            AgentCompanyName = a.Agent.CompanyName,
            SubcontractorCompanyName = a.Subcontractor.CompanyName,
            SubcontractorProfileId = a.Subcontractor.Id,
            Progress = a.Progress,
            Status = a.Status,
            StartDate = a.StartDate,
            DueDate = a.DueDate,
            CompletedAt = a.CompletedAt,
            CreatedAt = a.CreatedAt,
            OfferPrice = a.Offer?.Price ?? 0,
            OfferCurrency = a.Offer?.Currency ?? "TRY",
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
            UserId = subUserId,
            Type = "REPORT_REQUESTED",
            Title = "Rapor Talebi",
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
        a.Status = "completed"; a.Progress = 100; a.CompletedAt = DateTime.UtcNow; a.UpdatedAt = DateTime.UtcNow;
        a.JobListing.Status = "completed"; a.JobListing.UpdatedAt = DateTime.UtcNow;
        a.Subcontractor.TotalCompleted++;
        if (a.Offer != null)
        {
            _db.WalletTransactions.Add(new WalletTransaction
            {
                SubcontractorId = a.SubcontractorId,
                AssignedJobId = a.Id,
                Type = "earning",
                Amount = a.Offer.Price,
                Currency = a.Offer.Currency,
                Status = "completed",
                Description = $"Hakediş: {a.JobListing.Title}"
            });
        }
        _db.JobLogs.Add(new JobLog { AssignedJobId = a.Id, CreatedBy = userId, Title = "İş tamamlandı", Description = "Acente onayladı." });
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        var subUserId = await _db.SubcontractorProfiles.Where(s => s.Id == a.SubcontractorId).Select(s => s.UserId).FirstAsync();
        _db.Notifications.Add(new Notification
        {
            UserId = subUserId,
            Type = "JOB_COMPLETED",
            Title = "İş Tamamlandı",
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

    // ─── SUBCONTRACTORS ──────────────────────────────────────────────────────

    public async Task<List<Portlink.Api.Modules.Auth.Dtos.SubcontractorProfileResponse>> GetSubcontractorsAsync(string? search)
    {
        var query = _db.SubcontractorProfiles.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.ToLower();
            query = query.Where(s => s.CompanyName.ToLower().Contains(search) || 
                                     (s.City != null && s.City.ToLower().Contains(search)));
        }
        return await query.Select(s => new Portlink.Api.Modules.Auth.Dtos.SubcontractorProfileResponse
        {
            Id = s.Id, FullName = s.FullName, CompanyName = s.CompanyName,
            Phone = s.Phone, Country = s.Country, City = s.City,
            LogoUrl = s.LogoUrl, Rating = s.Rating, TotalCompleted = s.TotalCompleted,
            ExpertiseTags = s.ExpertiseTags != null ? s.ExpertiseTags.ToList() : new List<string>(),
            IsVerified = s.IsVerified
        }).ToListAsync();
    }

    public async Task<Portlink.Api.Modules.Auth.Dtos.SubcontractorProfileResponse> GetSubcontractorByIdAsync(Guid userId, Guid subcontractorId)
    {
        var s = await _db.SubcontractorProfiles
            .Include(sp => sp.User)
            .FirstOrDefaultAsync(sp => sp.Id == subcontractorId)
            ?? throw new KeyNotFoundException("Taşeron bulunamadı.");

        var hasRated = await _db.Ratings
            .AnyAsync(r => r.RaterUserId == userId && r.RateeProfileId == subcontractorId);

        var breakdownRaw = await _db.Ratings
            .Where(r => r.RateeProfileId == subcontractorId)
            .GroupBy(r => (int)Math.Round((double)r.Score))
            .Select(g => new { Star = g.Key, Count = g.Count() })
            .ToListAsync();

        var breakdown = new Dictionary<int, int> { {1,0},{2,0},{3,0},{4,0},{5,0} };
        foreach (var b in breakdownRaw)
            if (b.Star >= 1 && b.Star <= 5) breakdown[b.Star] = b.Count;

        return new Portlink.Api.Modules.Auth.Dtos.SubcontractorProfileResponse
        {
            Id = s.Id, FullName = s.FullName, CompanyName = s.CompanyName,
            Email = s.User?.Email, Phone = s.Phone, Country = s.Country, City = s.City,
            LogoUrl = s.LogoUrl, Rating = s.Rating, RatingCount = s.RatingCount,
            TotalCompleted = s.TotalCompleted,
            ExpertiseTags = s.ExpertiseTags != null ? s.ExpertiseTags.ToList() : new List<string>(),
            IsVerified = s.IsVerified,
            HasCurrentUserRated = hasRated,
            RatingBreakdown = breakdown
        };
    }

    public async Task RateSubcontractorAsync(Guid userId, Guid subcontractorId, decimal rating)
    {
        await GetAgentProfileAsync(userId);
        var sub = await _db.SubcontractorProfiles.FirstOrDefaultAsync(s => s.Id == subcontractorId)
            ?? throw new KeyNotFoundException("Taşeron bulunamadı.");

        var alreadyRated = await _db.Ratings
            .AnyAsync(r => r.RaterUserId == userId && r.RateeProfileId == subcontractorId);
        if (alreadyRated)
            throw new InvalidOperationException("Bu taşeronu daha önce puanladınız.");

        sub.Rating = (sub.Rating * sub.RatingCount + rating) / (sub.RatingCount + 1);
        sub.RatingCount++;

        _db.Ratings.Add(new Portlink.Api.Entities.Rating
        {
            RaterUserId = userId,
            RateeProfileId = subcontractorId,
            Score = rating
        });

        await _db.SaveChangesAsync();
    }

    // ─── PRIVATE ──────────────────────────────────────────────────────────────

    private async Task<AgentProfile> GetAgentProfileAsync(Guid userId)
        => await _db.AgentProfiles.FirstOrDefaultAsync(a => a.UserId == userId)
           ?? throw new UnauthorizedAccessException("Acente profili bulunamadı.");

    private static IQueryable<Offer> ApplyOfferSorting(IQueryable<Offer> query, string? sortBy, string? sortDirection)
    {
        var descending = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        return (sortBy ?? "createdAt").ToLower() switch
        {
            "price" => descending
                ? query.OrderByDescending(o => o.Price).ThenByDescending(o => o.CreatedAt)
                : query.OrderBy(o => o.Price).ThenBy(o => o.CreatedAt),
            "status" => descending
                ? query.OrderByDescending(o => o.Status).ThenByDescending(o => o.CreatedAt)
                : query.OrderBy(o => o.Status).ThenBy(o => o.CreatedAt),
            "jobtitle" => descending
                ? query.OrderByDescending(o => o.JobListing.Title).ThenByDescending(o => o.CreatedAt)
                : query.OrderBy(o => o.JobListing.Title).ThenBy(o => o.CreatedAt),
            _ => descending
                ? query.OrderByDescending(o => o.CreatedAt)
                : query.OrderBy(o => o.CreatedAt)
        };
    }

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

    private static AssignedJobResponse MapAssignedJob(AssignedJob a) => new()
    {
        Id = a.Id,
        JobId = a.JobId,
        JobTitle = a.JobListing?.Title ?? string.Empty,
        AgentUserId = a.Agent?.UserId ?? Guid.Empty,
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
}
