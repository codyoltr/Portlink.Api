using System.ComponentModel.DataAnnotations;

namespace Portlink.Api.Entities;

public class JobLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AssignedJobId { get; set; }
    public AssignedJob AssignedJob { get; set; } = null!;

    public Guid CreatedBy { get; set; }
    public User Creator { get; set; } = null!;

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
