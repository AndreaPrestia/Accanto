namespace Accanto.Application.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, ClientInfo? client = null, CancellationToken cancellationToken = default);
    Task<LoginResult> LoginAsync(LoginRequest request, ClientInfo? client = null, CancellationToken cancellationToken = default);
    Task<AuthResponse> CompleteTwoFactorAsync(TwoFactorLoginRequest request, ClientInfo? client = null, CancellationToken cancellationToken = default);
    Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, ClientInfo? client = null, CancellationToken cancellationToken = default);
    Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> GetMeAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Avvia il flusso di reset password: se l'email corrisponde a un account,
    /// emette un token monouso e invia il link via email. Per evitare
    /// enumerazione delle email, il metodo restituisce sempre senza errori
    /// anche quando l'account non esiste.
    /// </summary>
    Task RequestPasswordResetAsync(ForgotPasswordRequest request, ClientInfo? client = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completa il reset: valida il token, aggiorna l'hash della password,
    /// marca il token come usato e revoca tutte le sessioni attive.
    /// </summary>
    Task ResetPasswordAsync(ResetPasswordRequest request, ClientInfo? client = null, CancellationToken cancellationToken = default);
}
