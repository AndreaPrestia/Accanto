namespace Accanto.Application.Auth.TwoFactor;

public interface ITwoFactorService
{
    Task<TwoFactorStatusDto> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<TwoFactorSetupResponse> SetupAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<EnableTwoFactorResponse> EnableAsync(Guid userId, EnableTwoFactorRequest request, CancellationToken cancellationToken = default);
    Task DisableAsync(Guid userId, DisableTwoFactorRequest request, CancellationToken cancellationToken = default);
    Task<EnableTwoFactorResponse> RegenerateRecoveryCodesAsync(Guid userId, RegenerateRecoveryCodesRequest request, CancellationToken cancellationToken = default);

    /// <summary>Verifica un codice TOTP per l'utente; usato nel flusso di login challenge.</summary>
    bool VerifyCode(string secret, string code);

    /// <summary>Verifica un codice TOTP per l'utente caricando e decifrando il segreto.</summary>
    Task<bool> VerifyUserCodeAsync(Guid userId, string code, CancellationToken cancellationToken = default);

    /// <summary>Consuma un recovery code (rimuovendolo) se valido; ritorna true se trovato.</summary>
    Task<bool> ConsumeRecoveryCodeAsync(Guid userId, string code, CancellationToken cancellationToken = default);
}
