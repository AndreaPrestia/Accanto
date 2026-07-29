using Accanto.Admin.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accanto.Admin.Infrastructure.Persistence.Configurations;

public class AdminUserRoleConfiguration : IEntityTypeConfiguration<AdminUserRole>
{
    public void Configure(EntityTypeBuilder<AdminUserRole> b)
    {
        b.ToTable("admin_user_roles");
        b.HasKey(x => x.Id);
        b.Property(x => x.AdminUserId).IsRequired();
        b.Property(x => x.AdminRoleId).IsRequired();

        b.HasIndex(x => new { x.AdminUserId, x.AdminRoleId }).IsUnique();
        b.HasIndex(x => x.AdminRoleId);
    }
}
