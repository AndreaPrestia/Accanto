namespace Accanto.Application.Documents;

/// <summary>
/// Configurazione del worker di replica documenti verso S3 (IONOS o
/// AWS-compatibile). Sezione appsettings: "S3DocumentReplica".
///
/// Se Enabled=false (default) l'upload resta solo su disco e il worker
/// non parte. Compatibile con dev/test.
/// </summary>
public class S3DocumentReplicaOptions
{
    public bool Enabled { get; set; }

    /// <summary>Endpoint S3 (vuoto = AWS S3 default).
    /// Es. IONOS: "https://s3-eu-central-1.ionoscloud.com".</summary>
    public string? ServiceUrl { get; set; }

    public string Region { get; set; } = "us-east-1";
    public string Bucket { get; set; } = string.Empty;

    /// <summary>Prefisso (cartella) nel bucket. Default "storage/".
    /// Convenzione: NO Object Lock su questo prefisso (i documenti
    /// devono restare GDPR-erasable).</summary>
    public string Prefix { get; set; } = "storage/";

    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;

    /// <summary>Intervallo di polling dell'outbox (secondi). Default 10.</summary>
    public int PollIntervalSeconds { get; set; } = 10;

    /// <summary>Quante righe processare per ciclo. Default 10.</summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>Numero massimo di tentativi prima di marcare 'failed'.
    /// Default 5.</summary>
    public int MaxRetries { get; set; } = 5;
}
