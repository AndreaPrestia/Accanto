namespace Accanto.Admin.Application.Auth;

public interface IAdminPasswordResetService
{
    /// <summary>
    /// Avvia il reset: se l'email corrisponde a un admin attivo emette un token
    /// monouso e invia il link via email. Anti-enumerazione: ritorna sempre senza
    /// errori anche se l'admin non esiste.
    /// </summary>
    Task RequestResetAsync(AdminForgotPasswordRequest request, AdminClientInfo? client = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completa il reset: valida il token, imposta la nuova password, marca il
    /// token usato e revoca tutte le sessioni admin dell'utente.
    /// </summary>
    Task ResetAsync(AdminResetPasswordRequest request, AdminClientInfo? client = null, CancellationToken cancellationToken = default);
}
