using System.ComponentModel.DataAnnotations;

namespace Portlink.Api.Entities;

public class Port
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;  // TRALI-001

    [Required, MaxLength(300)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Region { get; set; }  // İstanbul, İzmir, Mersin...

    /// <summary>Koordinatlar: "lat,lng" formatında saklanır (PostgreSQL POINT yerine string)</summary>
    public string? Coordinates { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<JobListing> JobListings { get; set; } = new List<JobListing>();
}
