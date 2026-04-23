namespace Portlink.Api.DTOs.Jobs;

// ──────────────────── REQUEST ────────────────────

public class CreateJobListingRequest
{
    public string Title { get; set; } = string.Empty;
    public string ListingType { get; set; } = "subcontractor";  // subcontractor | agency-partnership
    public string? ShipName { get; set; }
    public string? PortCode { get; set; }
    public string? PortName { get; set; }
    public string? Location { get; set; }
    public string Category { get; set; } = string.Empty;
    public List<string> SelectedServices { get; set; } = new();
    public DateTime? Eta { get; set; }
    public string? NeedText { get; set; }
    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public string Currency { get; set; } = "TRY";
    public DateTime? Deadline { get; set; }
}

public class UpdateJobListingRequest
{
    public string? Title { get; set; }
    public string? ShipName { get; set; }
    public string? PortCode { get; set; }
    public string? Location { get; set; }
    public string? Category { get; set; }
    public List<string>? SelectedServices { get; set; }
    public DateTime? Eta { get; set; }
    public string? NeedText { get; set; }
    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public string? Currency { get; set; }
    public DateTime? Deadline { get; set; }
    public string? Status { get; set; }
}

public class UpdateAssignedJobRequest
{
    public int? Progress { get; set; }
    public string? Status { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
}

public class AddJobLogRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}

// ──────────────────── RESPONSE ───────────────────

public class JobListingResponse
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public string AgentCompanyName { get; set; } = string.Empty;
    public string? AgentLogoUrl { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ListingType { get; set; } = string.Empty;
    public string? ShipName { get; set; }
    public string? PortCode { get; set; }
    public string? PortName { get; set; }
    public string? Location { get; set; }
    public string Category { get; set; } = string.Empty;
    public List<string> SelectedServices { get; set; } = new();
    public DateTime? Eta { get; set; }
    public string? NeedText { get; set; }
    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public string Currency { get; set; } = "TRY";
    public string Status { get; set; } = string.Empty;
    public int OfferCount { get; set; }
    public DateTime? Deadline { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class JobListingDetailResponse : JobListingResponse
{
    public List<JobFileResponse> Files { get; set; } = new();
}

public class JobFileResponse
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long? FileSize { get; set; }
    public string? FileType { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AssignedJobResponse
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string AgentCompanyName { get; set; } = string.Empty;
    public string SubcontractorCompanyName { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AssignedJobDetailResponse : AssignedJobResponse
{
    public List<JobLogResponse> Logs { get; set; } = new();
    public List<JobReportResponse> Reports { get; set; } = new();
}

public class JobLogResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class JobReportResponse
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long? FileSize { get; set; }
    public string? FileType { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AgentDashboardStatsResponse
{
    public int ActiveListings { get; set; }
    public int TotalOffers { get; set; }
    public int ActiveJobs { get; set; }
    public int CompletedJobs { get; set; }
}

public class SubcontractorDashboardStatsResponse
{
    public int ActiveBids { get; set; }
    public int AcceptedBids { get; set; }
    public int ActiveJobs { get; set; }
    public int CompletedJobs { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal PendingEarnings { get; set; }
}
