using Accanto.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accanto.Infrastructure.Persistence.Configurations;

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> b)
    {
        b.ToTable("push_subscriptions");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.Endpoint).IsRequired().HasMaxLength(2048);
        b.Property(x => x.P256dh).IsRequired().HasMaxLength(256);
        b.Property(x => x.Auth).IsRequired().HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(500);
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.LastUsedAt).IsRequired();
        b.HasIndex(x => x.UserId);
        b.HasIndex(x => new { x.UserId, x.Endpoint }).IsUnique();
    }
}
