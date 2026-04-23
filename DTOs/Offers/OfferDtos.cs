namespace Portlink.Api.DTOs.Offers;

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
