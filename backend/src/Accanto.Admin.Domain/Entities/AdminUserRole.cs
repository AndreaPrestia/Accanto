namespace Accanto.Admin.Domain.Entities;

/// <summary>Associazione molti-a-molti tra <see cref="AdminUser"/> e <see cref="AdminRole"/>.</summary>
public sealed class AdminUserRole
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }
    public Guid AdminRoleId { get; set; }

    public AdminUser AdminUser { get; set; } = default!;
    public AdminRole AdminRole { get; set; } = default!;
}
