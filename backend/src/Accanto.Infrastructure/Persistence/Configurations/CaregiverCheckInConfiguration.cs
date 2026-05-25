using Accanto.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accanto.Infrastructure.Persistence.Configurations;

public class CaregiverCheckInConfiguration : IEntityTypeConfiguration<CaregiverCheckIn>
{
    public void Configure(EntityTypeBuilder<CaregiverCheckIn> b)
    {
        b.ToTable("caregiver_check_ins");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.Mood).IsRequired();
        b.Property(x => x.Energy).IsRequired();
        b.Property(x => x.Stress).IsRequired();
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.CreatedAt).IsRequired();
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}
