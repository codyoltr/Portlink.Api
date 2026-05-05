using System.ComponentModel.DataAnnotations;
using Portlink.Api.Modules.Auth.Entities;

namespace Portlink.Api.Entities;

public class Conversation
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AgentId { get; set; }
    public AgentProfile Agent { get; set; } = null!;

    public Guid SubcontractorId { get; set; }
    public SubcontractorProfile Subcontractor { get; set; } = null!;

    public Guid? JobListingId { get; set; }
    public JobListing? JobListing { get; set; }

    [MaxLength(300)]
    public string? LastMessagePreview { get; set; }

    public DateTime? LastMessageAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ConversationMessage> Messages { get; set; } = new List<ConversationMessage>();
}
