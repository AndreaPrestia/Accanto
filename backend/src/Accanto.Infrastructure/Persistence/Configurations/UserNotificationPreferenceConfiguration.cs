using Accanto.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accanto.Infrastructure.Persistence.Configurations;

public class UserNotificationPreferenceConfiguration : IEntityTypeConfiguration<UserNotificationPreference>
{
    public void Configure(EntityTypeBuilder<UserNotificationPreference> b)
    {
        b.ToTable("user_notification_preferences");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.Topic).IsRequired().HasConversion<string>().HasMaxLength(64);
        b.Property(x => x.EmailEnabled).IsRequired();
        b.Property(x => x.PushEnabled).IsRequired().HasDefaultValue(true);
        b.Property(x => x.UpdatedAt).IsRequired();
        b.HasIndex(x => new { x.UserId, x.Topic }).IsUnique();
    }
}
