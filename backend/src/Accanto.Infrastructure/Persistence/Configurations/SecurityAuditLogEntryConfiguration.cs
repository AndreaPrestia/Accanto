using Accanto.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accanto.Infrastructure.Persistence.Configurations;

public class SecurityAuditLogEntryConfiguration : IEntityTypeConfiguration<SecurityAuditLogEntry>
{
    public void Configure(EntityTypeBuilder<SecurityAuditLogEntry> b)
    {
        b.ToTable("security_audit_log_entries");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId);
        b.Property(x => x.EmailAttempted).HasMaxLength(320);
        b.Property(x => x.EventType).IsRequired().HasConversion<string>().HasMaxLength(64);
        b.Property(x => x.Summary).HasMaxLength(500);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(500);
        b.Property(x => x.Timestamp).IsRequired();
        b.HasIndex(x => new { x.UserId, x.Timestamp });
        b.HasIndex(x => x.Timestamp);
    }
}
