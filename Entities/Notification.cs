using System.ComponentModel.DataAnnotations;

namespace Portlink.Api.Entities;

public class Notification
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>
    /// NEW_OFFER | OFFER_ACCEPTED | OFFER_REJECTED |
    /// NEW_MESSAGE | JOB_COMPLETED | REPORT_REQUESTED | NEW_REPORT
    /// </summary>
    [Required, MaxLength(50)]
    public string Type { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    public string? Body { get; set; }

    public bool IsRead { get; set; } = false;

    /// <summary>İlgili entity id vb. — JSON string (jsonb kolonu)</summary>
    public string? Data { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
