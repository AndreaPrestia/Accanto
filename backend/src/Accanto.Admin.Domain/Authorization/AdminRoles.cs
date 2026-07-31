namespace Accanto.Admin.Domain.Authorization;

/// <summary>
/// Nomi canonici dei ruoli amministrativi (seed in AccantoAdminDb).
/// Usare queste costanti invece di stringhe letterali sparse.
/// </summary>
public static class AdminRoles
{
    /// <summary>Accesso completo al control plane, incluse le operazioni piu' sensibili.</summary>
    public const string Owner = "Owner";

    /// <summary>Operazioni tecniche di routine sugli account (disable/enable/revoke).</summary>
    public const string Operator = "Operator";

    /// <summary>Sola lettura di audit log e stato tecnico. Nessuna operazione mutativa.</summary>
    public const string SecurityAuditor = "SecurityAuditor";

    /// <summary>Tutti i ruoli seed, nell'ordine di definizione.</summary>
    public static readonly IReadOnlyList<string> All = new[] { Owner, Operator, SecurityAuditor };
}
