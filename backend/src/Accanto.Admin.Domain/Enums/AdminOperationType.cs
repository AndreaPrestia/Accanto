namespace Accanto.Admin.Domain.Enums;

/// <summary>
/// Tipo di operazione tecnica che un admin puo' richiedere sulla piattaforma.
/// Solo operazioni su metadata/account: MAI lettura di contenuti utente.
/// </summary>
public enum AdminOperationType
{
    DisableUser = 1,
    EnableUser = 2,
    RevokeUserSessions = 3,
    StartUserDeletion = 4
}
