using Accanto.Domain.Enums;

namespace Accanto.Domain.Entities;

public class CareCircleMember
{
    public Guid Id { get; set; }
    public Guid CareCircleId { get; set; }
    public Guid UserId { get; set; }
    public CareCircleRole Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
