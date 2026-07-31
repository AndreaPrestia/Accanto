using Accanto.Admin.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accanto.Admin.Infrastructure.Persistence.Configurations;

public class AdminRoleConfiguration : IEntityTypeConfiguration<AdminRole>
{
    public void Configure(EntityTypeBuilder<AdminRole> b)
    {
        b.ToTable("admin_roles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(64);

        b.HasIndex(x => x.Name).IsUnique();

        b.HasMany(x => x.UserRoles)
            .WithOne(x => x.AdminRole)
            .HasForeignKey(x => x.AdminRoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
