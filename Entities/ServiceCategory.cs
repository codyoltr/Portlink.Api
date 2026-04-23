using System.ComponentModel.DataAnnotations;

namespace Portlink.Api.Entities;

public class ServiceCategory
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;  // 'port-operations'

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;  // 'Liman İşlemleri'

    /// <summary>Alt hizmet seçenekleri — PostgreSQL text[]</summary>
    public List<string> SubServices { get; set; } = new List<string>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
