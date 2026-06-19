namespace Accanto.Application.Common.Storage;

/// <summary>
/// Replica del file storage verso uno storage S3-compatibile.
/// Implementazione tipica: leggere il blob CIFRATO da disco
/// (LocalFileStorage non espone direttamente lo stream cifrato, quindi
/// l'implementazione legge il file fisico bypassando IFileStorage)
/// e farne PUT con la stessa key relativa.
///
/// Le delete devono cancellare TUTTE le versioni S3 (bucket versionato)
/// per onorare GDPR right-to-erasure su documenti utente.
/// </summary>
public interface IS3DocumentReplica
{
    /// <summary>Carica il file presente sul filesystem locale (path
    /// relativo al RootPath dello storage) verso S3 con la stessa
    /// chiave relativa.</summary>
    Task PutAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>Cancella tutte le versioni e i delete-marker della key
    /// dato lo storage_path. Sui bucket versionati una semplice
    /// DeleteObject lascia la versione originale recuperabile, quindi
    /// per GDPR serve l'enumerazione esplicita.</summary>
    Task DeleteAllVersionsAsync(string storagePath, CancellationToken cancellationToken = default);
}
