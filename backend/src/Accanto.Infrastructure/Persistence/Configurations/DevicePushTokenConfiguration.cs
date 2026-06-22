using Accanto.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accanto.Infrastructure.Persistence.Configurations;

public class DevicePushTokenConfiguration : IEntityTypeConfiguration<DevicePushToken>
{
    public void Configure(EntityTypeBuilder<DevicePushToken> b)
    {
        b.ToTable("device_push_tokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).IsRequired();
        // Il token Expo è del tipo `ExponentPushToken[xxx]`; tipicamente 40-60 char,
        // ma teniamo margine per future varianti.
        b.Property(x => x.Token).IsRequired().HasMaxLength(256);
        b.Property(x => x.Platform).IsRequired().HasMaxLength(16);
        b.Property(x => x.DeviceName).HasMaxLength(128);
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.LastUsedAt).IsRequired();

        // Un token Expo è univoco per device-app installation: due utenti
        // non possono condividere lo stesso token (e se succede l'upsert
        // riassegna l'ownership).
        b.HasIndex(x => x.Token).IsUnique();
        b.HasIndex(x => x.UserId);
    }
}
