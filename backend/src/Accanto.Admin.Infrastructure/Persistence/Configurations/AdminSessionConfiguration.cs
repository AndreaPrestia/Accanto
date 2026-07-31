using Accanto.Admin.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accanto.Admin.Infrastructure.Persistence.Configurations;

public class AdminSessionConfiguration : IEntityTypeConfiguration<AdminSession>
{
    public void Configure(EntityTypeBuilder<AdminSession> b)
    {
        b.ToTable("admin_sessions");
        b.HasKey(x => x.Id);
        b.Property(x => x.AdminUserId).IsRequired();
        // SHA-256 esadecimale = 64 caratteri; mai salvare il token raw.
        b.Property(x => x.RefreshTokenHash).IsRequired().HasMaxLength(128);
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.ExpiresAt).IsRequired();
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(500);

        b.HasIndex(x => x.AdminUserId);
        b.HasIndex(x => x.RefreshTokenHash);
        b.HasIndex(x => x.ExpiresAt);
    }
}
