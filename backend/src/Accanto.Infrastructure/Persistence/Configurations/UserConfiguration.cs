using Accanto.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accanto.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Email).IsRequired().HasMaxLength(256);
        b.Property(x => x.DisplayName).IsRequired().HasMaxLength(120);
        b.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
        b.Property(x => x.Language).HasMaxLength(8);
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.IsErased).IsRequired();
        b.Property(x => x.ErasureReason).HasMaxLength(500);
        b.Property(x => x.IsDisabled).IsRequired();
        b.Property(x => x.DisabledReason).HasMaxLength(500);
        b.HasIndex(x => x.Email).IsUnique();
    }
}
