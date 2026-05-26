using Accanto.Domain.Enums;

namespace Accanto.Domain.Entities;

public class CareCircle
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CareCircleStatus Status { get; set; } = CareCircleStatus.Active;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Se true, le funzioni di assistenza AI sono abilitate per questo cerchio di cura.
    /// Default false: opt-in esplicito dell'owner.
    /// </summary>
    public bool AiEnabled { get; set; } = false;

    public List<CareCircleMember> Members { get; set; } = new();
}
