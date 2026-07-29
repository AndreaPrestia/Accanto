using Accanto.Admin.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accanto.Admin.Infrastructure.Persistence.Configurations;

public class AdminOperationConfiguration : IEntityTypeConfiguration<AdminOperation>
{
    public void Configure(EntityTypeBuilder<AdminOperation> b)
    {
        b.ToTable("admin_operations");
        b.HasKey(x => x.Id);
        b.Property(x => x.RequestedByAdminUserId).IsRequired();
        b.Property(x => x.OperationType).IsRequired().HasConversion<string>().HasMaxLength(64);
        // Riferimento opaco all'utente pubblico: nessun dato sensibile copiato.
        b.Property(x => x.TargetUserId);
        b.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Reason).IsRequired().HasMaxLength(500);
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.ErrorMessage).HasMaxLength(1000);

        b.HasIndex(x => x.TargetUserId);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.OperationType);
        b.HasIndex(x => x.CreatedAt);

        b.HasOne(x => x.RequestedByAdminUser)
            .WithMany()
            .HasForeignKey(x => x.RequestedByAdminUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
