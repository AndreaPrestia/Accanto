using Accanto.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accanto.Infrastructure.Persistence.Configurations;

public class CareCircleInviteConfiguration : IEntityTypeConfiguration<CareCircleInvite>
{
    public void Configure(EntityTypeBuilder<CareCircleInvite> b)
    {
        b.ToTable("care_circle_invites");
        b.HasKey(x => x.Id);

        b.Property(x => x.CareCircleId).IsRequired();
        b.Property(x => x.CreatedByUserId).IsRequired();
        b.Property(x => x.Token).IsRequired().HasMaxLength(64);
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.ExpiresAt).IsRequired();
        b.Property(x => x.MaxUses).IsRequired();
        b.Property(x => x.UsedCount).IsRequired();
        b.Property(x => x.RevokedAt);
        b.Property(x => x.CreatedAt).IsRequired();

        b.HasIndex(x => x.Token).IsUnique();
        b.HasIndex(x => x.CareCircleId);
    }
}
