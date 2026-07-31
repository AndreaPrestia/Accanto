using Accanto.Admin.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accanto.Admin.Infrastructure.Persistence.Configurations;

public class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
{
    public void Configure(EntityTypeBuilder<AdminAuditLog> b)
    {
        b.ToTable("admin_audit_logs");
        b.HasKey(x => x.Id);
        b.Property(x => x.AdminUserId).IsRequired();
        b.Property(x => x.Action).IsRequired().HasMaxLength(128);
        b.Property(x => x.TargetType).IsRequired().HasMaxLength(64);
        // Identificativo opaco del target (GUID/testo breve), mai contenuto utente.
        b.Property(x => x.TargetId).HasMaxLength(128);
        // Motivazione obbligatoria per azioni mutative, lunghezza limitata.
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(500);
        b.Property(x => x.CreatedAt).IsRequired();

        b.HasIndex(x => x.AdminUserId);
        b.HasIndex(x => x.Action);
        b.HasIndex(x => x.TargetType);
        b.HasIndex(x => x.TargetId);
        b.HasIndex(x => x.CreatedAt);

        b.HasOne(x => x.AdminUser)
            .WithMany()
            .HasForeignKey(x => x.AdminUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
