using Accanto.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accanto.Infrastructure.Persistence.Configurations;

public class DocumentSyncOutboxEntryConfiguration : IEntityTypeConfiguration<DocumentSyncOutboxEntry>
{
    public void Configure(EntityTypeBuilder<DocumentSyncOutboxEntry> b)
    {
        b.ToTable("document_sync_outbox");
        b.HasKey(x => x.Id);
        b.Property(x => x.DocumentId);
        b.Property(x => x.StoragePath).IsRequired().HasMaxLength(500);
        b.Property(x => x.Operation).IsRequired().HasMaxLength(10);
        b.Property(x => x.Status).IsRequired().HasMaxLength(20);
        b.Property(x => x.RetryCount).IsRequired();
        b.Property(x => x.LastError).HasMaxLength(1000);
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt).IsRequired();
        b.Property(x => x.NextAttemptAt).IsRequired();

        // Indice per la query del worker: cerca pending/in_progress
        // ordinati per next_attempt_at.
        b.HasIndex(x => new { x.Status, x.NextAttemptAt });
    }
}
