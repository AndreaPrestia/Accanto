namespace Accanto.Domain.Entities;

public class CaregiverCheckIn
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public short Mood { get; set; }
    public short Energy { get; set; }
    public short Stress { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
