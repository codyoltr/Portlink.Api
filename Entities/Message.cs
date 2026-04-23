using System.ComponentModel.DataAnnotations;

namespace Portlink.Api.Entities;

public class Message
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? AssignedJobId { get; set; }
    public AssignedJob? AssignedJob { get; set; }

    public Guid SenderId { get; set; }
    public User Sender { get; set; } = null!;

    public Guid ReceiverId { get; set; }
    public User Receiver { get; set; } = null!;

    [Required]
    public string Content { get; set; } = string.Empty;

    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
