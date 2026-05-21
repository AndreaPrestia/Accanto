using Accanto.Domain.Enums;

namespace Accanto.Application.Common.Authorization;

public interface ICareCircleAuthorization
{
    /// <summary>
    /// Ensures the user is a member of the care circle and has at least the required role.
    /// Throws ForbiddenException otherwise. Throws NotFoundException if the circle does not exist.
    /// </summary>
    Task EnsureMemberAsync(Guid userId, Guid careCircleId, CareCircleRole minimumRole, CancellationToken cancellationToken = default);
}
