using Accanto.Domain.Enums;

namespace Accanto.Application.SharedUpdates;

public sealed record CreateSharedUpdateRequest(SharedUpdateAudience Audience, string Content);

public sealed record SharedUpdateDto(
    Guid Id,
    Guid CareCircleId,
    Guid CreatedByUserId,
    SharedUpdateAudience Audience,
    string Content,
    DateTimeOffset CreatedAt
);

public sealed record SharedUpdateTemplateDto(string Title, string Content);
