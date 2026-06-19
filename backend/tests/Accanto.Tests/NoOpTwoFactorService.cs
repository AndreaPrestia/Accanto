using Accanto.Application.Auth.TwoFactor;

namespace Accanto.Tests;

/// <summary>
/// Test double: 2FA sempre disabilitato. Le verifiche tornano false
/// (non chiamate dai test che non hanno 2FA attivo sull'utente).
/// </summary>
public class NoOpTwoFactorService : ITwoFactorService
{
    public Task<TwoFactorStatusDto> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult(new TwoFactorStatusDto(false, 0));

    public Task<TwoFactorSetupResponse> SetupAsync(Guid userId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<EnableTwoFactorResponse> EnableAsync(Guid userId, EnableTwoFactorRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task DisableAsync(Guid userId, DisableTwoFactorRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<EnableTwoFactorResponse> RegenerateRecoveryCodesAsync(Guid userId, RegenerateRecoveryCodesRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public bool VerifyCode(string secret, string code) => false;
    public Task<bool> VerifyUserCodeAsync(Guid userId, string code, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<bool> ConsumeRecoveryCodeAsync(Guid userId, string code, CancellationToken cancellationToken = default) => Task.FromResult(false);
}
