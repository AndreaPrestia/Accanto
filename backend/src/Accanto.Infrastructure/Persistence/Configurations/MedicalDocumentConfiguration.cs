using Accanto.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accanto.Infrastructure.Persistence.Configurations;

public class MedicalDocumentConfiguration : IEntityTypeConfiguration<MedicalDocument>
{
    public void Configure(EntityTypeBuilder<MedicalDocument> b)
    {
        b.ToTable("medical_documents");
        b.HasKey(x => x.Id);
        b.Property(x => x.CareCircleId).IsRequired();
        b.Property(x => x.UploadedByUserId).IsRequired();
        b.Property(x => x.Category).HasConversion<string>().HasMaxLength(40).IsRequired();
        b.Property(x => x.FileName).IsRequired().HasMaxLength(260);
        b.Property(x => x.OriginalFileName).IsRequired().HasMaxLength(260);
        b.Property(x => x.ContentType).IsRequired().HasMaxLength(120);
        b.Property(x => x.SizeInBytes).IsRequired();
        b.Property(x => x.StoragePath).IsRequired().HasMaxLength(500);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.Tags).HasColumnType("text[]");

        b.HasIndex(x => x.CareCircleId);
    }
}
