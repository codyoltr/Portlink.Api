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

public class UpdateJobProgressRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public class AddJobLogRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class ReviewJobLogRequest
{
    public string? Note { get; set; }
}

// ──────────────────── RESPONSE ───────────────────

public class JobListingResponse
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public string AgentCompanyName { get; set; } = string.Empty;
    public string? AgentLogoUrl { get; set; }
    public Guid? ListingImageStorageFileId { get; set; }
    public string? ListingImagePreviewUrl { get; set; }
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
    public Guid? StorageFileId { get; set; }
    public string? PreviewUrl { get; set; }
    public long? FileSize { get; set; }
    public string? FileType { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AssignedJobResponse
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public Guid? ListingImageStorageFileId { get; set; }
    public string? ListingImagePreviewUrl { get; set; }
    public Guid AgentUserId { get; set; }
    public Guid AgentProfileId { get; set; }
    public Guid SubcontractorUserId { get; set; }
    public Guid SubcontractorProfileId { get; set; }
    public string AgentCompanyName { get; set; } = string.Empty;
    public string? AgentLogoUrl { get; set; }
    public string SubcontractorCompanyName { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal OfferPrice { get; set; }
    public string OfferCurrency { get; set; } = "TRY";
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
    public string Type { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public string? FileType { get; set; }
    public long? FileSize { get; set; }
    public string ReviewStatus { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
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
