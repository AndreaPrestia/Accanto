namespace Accanto.Infrastructure.Storage;

public class ClamAvOptions
{
    /// <summary>Host del demone clamd. Se vuoto/null l'AV resta disabilitato.</summary>
    public string? Host { get; set; }
    public int Port { get; set; } = 3310;
    /// <summary>Timeout per la connessione TCP + comando INSTREAM (sec).</summary>
    public int TimeoutSeconds { get; set; } = 30;
}
