using Accanto.Admin.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accanto.Admin.Infrastructure.Persistence.Configurations;

public class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
{
    public void Configure(EntityTypeBuilder<AdminUser> b)
    {
        b.ToTable("admin_users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Email).IsRequired().HasMaxLength(256);
        b.Property(x => x.DisplayName).IsRequired().HasMaxLength(120);
        b.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
        b.Property(x => x.MfaEnabled).IsRequired();
        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();

        b.HasIndex(x => x.Email).IsUnique();

        b.HasMany(x => x.Roles)
            .WithOne(x => x.AdminUser)
            .HasForeignKey(x => x.AdminUserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Sessions)
            .WithOne(x => x.AdminUser)
            .HasForeignKey(x => x.AdminUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
