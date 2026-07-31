namespace Accanto.Admin.Domain.Entities;

/// <summary>
/// Utente amministrativo della piattaforma. Vive ESCLUSIVAMENTE in AccantoAdminDb:
/// non ha alcuna relazione con la tabella pubblica `users` e non eredita nulla da essa.
/// Non esiste alcun flag "IsAdmin" sul dominio pubblico.
/// </summary>
public sealed class AdminUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool MfaEnabled { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    public ICollection<AdminUserRole> Roles { get; set; } = new List<AdminUserRole>();
    public ICollection<AdminSession> Sessions { get; set; } = new List<AdminSession>();
}
