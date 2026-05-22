namespace Accanto.Application.Invites;

public interface IInviteService
{
    Task<InviteDto> CreateAsync(Guid userId, Guid careCircleId, CreateInviteRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InviteDto>> ListAsync(Guid userId, Guid careCircleId, CancellationToken cancellationToken = default);
    Task RevokeAsync(Guid userId, Guid careCircleId, Guid inviteId, CancellationToken cancellationToken = default);
    Task<InvitePreviewDto> PreviewAsync(string token, CancellationToken cancellationToken = default);
    Task<Guid> AcceptAsync(Guid userId, string token, CancellationToken cancellationToken = default);
}
