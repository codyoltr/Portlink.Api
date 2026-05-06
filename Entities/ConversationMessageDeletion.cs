using System.ComponentModel.DataAnnotations;

namespace Portlink.Api.Entities;

public class ConversationMessageDeletion
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MessageId { get; set; }
    public ConversationMessage Message { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
}
