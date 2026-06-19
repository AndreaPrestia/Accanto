namespace Accanto.Domain.Entities;

/// <summary>
/// Coda di replica dei medical_documents verso storage S3 secondario
/// (IONOS). Inserita transazionalmente da DocumentService al momento di
/// upload/delete; consumata dal DocumentSyncWorker (BackgroundService).
///
/// Per le PUT: il file cifrato (lo stesso blob su disco) viene caricato
/// con prefisso configurato (default "storage/").
/// Per le DELETE: lo storage_path resta valorizzato perche' la
/// medical_documents puo' essere stata gia' rimossa.
/// </summary>
public class DocumentSyncOutboxEntry
{
    public Guid Id { get; set; }

    /// <summary>FK soft a medical_documents.Id. NULL dopo che la riga
    /// originale e' stata rimossa (DELETE outbox row aggiunta prima della
    /// rimozione del documento).</summary>
    public Guid? DocumentId { get; set; }

    /// <summary>Path relativo allo Storage:RootPath, es. "2026/06/uuid.pdf".
    /// Lo stesso valore della key S3 (modulo prefix configurato).</summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>"PUT" oppure "DELETE".</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>"pending" | "in_progress" | "done" | "failed".</summary>
    public string Status { get; set; } = "pending";

    public int RetryCount { get; set; }
    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
}
