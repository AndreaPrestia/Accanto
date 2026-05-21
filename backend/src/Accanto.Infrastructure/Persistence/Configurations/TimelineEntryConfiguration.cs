using Accanto.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accanto.Infrastructure.Persistence.Configurations;

public class TimelineEntryConfiguration : IEntityTypeConfiguration<TimelineEntry>
{
    public void Configure(EntityTypeBuilder<TimelineEntry> b)
    {
        b.ToTable("timeline_entries");
        b.HasKey(x => x.Id);
        b.Property(x => x.CareCircleId).IsRequired();
        b.Property(x => x.CreatedByUserId).IsRequired();
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
        b.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Title).IsRequired().HasMaxLength(200);
        b.Property(x => x.Content).HasMaxLength(10000);
        b.Property(x => x.OccurredAt).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.Tags).HasColumnType("text[]");

        b.HasIndex(x => new { x.CareCircleId, x.OccurredAt });
    }
}
