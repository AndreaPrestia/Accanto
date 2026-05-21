using Accanto.Domain.Enums;

namespace Accanto.Application.CareCircles;

public sealed record CreateCareCircleRequest(string Name, string? Description);
public sealed record UpdateCareCircleRequest(string Name, string? Description);

public sealed record CareCircleDto(
    Guid Id,
    string Name,
    string? Description,
    CareCircleStatus Status,
    CareCircleRole MyRole,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);
