namespace Accanto.Admin.Domain.Entities;

/// <summary>
/// Ruolo amministrativo (es. Owner, Operator, SecurityAuditor).
/// I nomi canonici sono in <see cref="Authorization.AdminRoles"/>.
/// </summary>
public sealed class AdminRole
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<AdminUserRole> UserRoles { get; set; } = new List<AdminUserRole>();
}
