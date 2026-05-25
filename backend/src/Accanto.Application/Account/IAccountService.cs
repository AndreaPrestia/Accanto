namespace Accanto.Application.Account;

public interface IAccountService
{
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, DeleteAccountRequest request, CancellationToken cancellationToken = default);
    Task UpdateLanguageAsync(Guid userId, UpdateLanguageRequest request, CancellationToken cancellationToken = default);
}
