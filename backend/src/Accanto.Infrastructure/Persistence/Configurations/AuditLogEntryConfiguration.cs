using Accanto.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accanto.Infrastructure.Persistence.Configurations;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> b)
    {
        b.ToTable("audit_log_entries");
        b.HasKey(x => x.Id);
        b.Property(x => x.CareCircleId).IsRequired();
        b.Property(x => x.PerformedByUserId).IsRequired();
        b.Property(x => x.ActionType).IsRequired().HasConversion<string>().HasMaxLength(64);
        b.Property(x => x.ResourceType).IsRequired().HasConversion<string>().HasMaxLength(64);
        b.Property(x => x.ResourceId);
        b.Property(x => x.Summary).HasMaxLength(500);
        b.Property(x => x.Timestamp).IsRequired();
        b.HasIndex(x => new { x.CareCircleId, x.Timestamp });
    }
}
