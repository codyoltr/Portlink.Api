namespace Portlink.Api.DTOs.Offers;

using Portlink.Api.Modules.Common.Dtos;

// ──────────────────── REQUEST ────────────────────

public class CreateOfferRequest
{
    public decimal Price { get; set; }
    public string Currency { get; set; } = "TRY";
    public int? EstimatedDays { get; set; }
    public string? CoverNote { get; set; }
}

// ──────────────────── RESPONSE ───────────────────

public class OfferResponse
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public Guid AgentUserId { get; set; }
    public Guid SubcontractorId { get; set; }
    public string SubcontractorCompanyName { get; set; } = string.Empty;
    public string? SubcontractorLogoUrl { get; set; }
    public decimal SubcontractorRating { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int? EstimatedDays { get; set; }
    public string? CoverNote { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AgentOffersQueryRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public string? Search { get; set; }
    public Guid? JobListingId { get; set; }
    public string? Location { get; set; }
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
}

public class AgentOfferListItemResponse
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string? ShipName { get; set; }
    public string AgentCompanyName { get; set; } = string.Empty;
    public string? Location { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int? EstimatedDays { get; set; }
    public string? CoverNote { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid SubcontractorId { get; set; }
    public string SubcontractorCompanyName { get; set; } = string.Empty;
    public string SubcontractorFullName { get; set; } = string.Empty;
    public string? SubcontractorLogoUrl { get; set; }
    public decimal SubcontractorRating { get; set; }
    public int SubcontractorCompletedJobsCount { get; set; }
    public bool SubcontractorIsVerified { get; set; }
}

public class AgentOfferDetailResponse : AgentOfferListItemResponse
{
    public string? PortName { get; set; }
    public string? PortCode { get; set; }
    public string? NeedText { get; set; }
    public string JobStatus { get; set; } = string.Empty;
    public DateTime? Deadline { get; set; }
}

public class AgentOffersDashboardResponse
{
    public PaginatedResponse<AgentOfferListItemResponse> Offers { get; set; } = new();
    public int TotalOffers { get; set; }
    public int PendingOffers { get; set; }
    public int AcceptedOffers { get; set; }
    public decimal? AverageOfferAmount { get; set; }
}
