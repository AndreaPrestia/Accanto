using Accanto.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accanto.Infrastructure.Persistence.Configurations;

public class CareCircleConfiguration : IEntityTypeConfiguration<CareCircle>
{
    public void Configure(EntityTypeBuilder<CareCircle> b)
    {
        b.ToTable("care_circles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(160);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedByUserId).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.AiEnabled).IsRequired().HasDefaultValue(false);

        b.HasMany(x => x.Members)
            .WithOne()
            .HasForeignKey(m => m.CareCircleId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.CreatedByUserId);
    }
}

public class CareCircleMemberConfiguration : IEntityTypeConfiguration<CareCircleMember>
{
    public void Configure(EntityTypeBuilder<CareCircleMember> b)
    {
        b.ToTable("care_circle_members");
        b.HasKey(x => x.Id);
        b.Property(x => x.CareCircleId).IsRequired();
        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();

        b.HasIndex(x => x.UserId);
        b.HasIndex(x => new { x.CareCircleId, x.UserId }).IsUnique();
    }
}
