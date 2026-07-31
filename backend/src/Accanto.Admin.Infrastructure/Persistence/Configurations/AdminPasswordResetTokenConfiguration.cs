using Accanto.Admin.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accanto.Admin.Infrastructure.Persistence.Configurations;

public class AdminPasswordResetTokenConfiguration : IEntityTypeConfiguration<AdminPasswordResetToken>
{
    public void Configure(EntityTypeBuilder<AdminPasswordResetToken> b)
    {
        b.ToTable("admin_password_reset_tokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.AdminUserId).IsRequired();
        // SHA-256 esadecimale = 64 caratteri; mai salvare il token raw.
        b.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.ExpiresAt).IsRequired();
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(500);

        b.HasIndex(x => x.TokenHash);
        b.HasIndex(x => x.AdminUserId);

        b.HasOne(x => x.AdminUser)
            .WithMany()
            .HasForeignKey(x => x.AdminUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
