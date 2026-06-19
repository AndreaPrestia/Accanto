namespace Accanto.Application.Account;

/// <summary>
/// Servizio GDPR right-to-erasure. Implementa la cancellazione
/// dell'utente in modalita' tombstone: PII azzerati, sessioni
/// revocate, documenti cancellati anche dalla replica S3, ma
/// l'audit log resta intatto per compliance/forensics.
/// </summary>
public interface IUserErasureService
{
    /// <summary>
    /// Esegue l'erasure dell'utente. Idempotente: se gia' tombstone
    /// non rifa' niente. <paramref name="reason"/> finisce sia nel
    /// campo User.ErasureReason sia nella security audit log.
    /// </summary>
    Task EraseAsync(Guid userId, string reason, CancellationToken cancellationToken = default);
}
