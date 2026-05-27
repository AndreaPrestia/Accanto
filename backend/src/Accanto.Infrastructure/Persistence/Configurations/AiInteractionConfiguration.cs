using Accanto.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accanto.Infrastructure.Persistence.Configurations;

public class AiInteractionConfiguration : IEntityTypeConfiguration<AiInteraction>
{
    public void Configure(EntityTypeBuilder<AiInteraction> b)
    {
        b.ToTable("ai_interactions");
        b.HasKey(x => x.Id);

        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.CareCircleId);

        b.Property(x => x.Function).HasConversion<string>().HasMaxLength(40).IsRequired();
        b.Property(x => x.Verdict).HasConversion<string>().HasMaxLength(32).IsRequired();
        b.Property(x => x.Feedback).HasConversion<string>().HasMaxLength(16);

        b.Property(x => x.Model).HasMaxLength(80).IsRequired();
        b.Property(x => x.PromptVersion).HasMaxLength(32).IsRequired();
        b.Property(x => x.Language).HasMaxLength(8).IsRequired();
        b.Property(x => x.TookMs).IsRequired();
        b.Property(x => x.CacheHit).IsRequired();

        // I campi cifrati sono testo (base64). Lunghezza massima conservativa.
        b.Property(x => x.InputJsonEncrypted).IsRequired();
        b.Property(x => x.OutputEncrypted).IsRequired();

        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.FeedbackAt);

        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasIndex(x => new { x.CareCircleId, x.CreatedAt });
    }
}
