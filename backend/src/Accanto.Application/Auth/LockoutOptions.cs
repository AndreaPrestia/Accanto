namespace Accanto.Application.Auth;

public class LockoutOptions
{
    /// <summary>Numero massimo di tentativi falliti consecutivi prima del blocco.</summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>Durata del blocco in minuti dopo aver superato il limite.</summary>
    public int LockoutMinutes { get; set; } = 15;

    /// <summary>
    /// Finestra (minuti) entro la quale i tentativi falliti vengono conteggiati come consecutivi.
    /// Oltre questa finestra dall'ultimo fallimento, il contatore viene azzerato.
    /// </summary>
    public int AttemptWindowMinutes { get; set; } = 15;
}
