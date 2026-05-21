using Accanto.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accanto.Infrastructure.Persistence.Configurations;

public class DoctorQuestionConfiguration : IEntityTypeConfiguration<DoctorQuestion>
{
    public void Configure(EntityTypeBuilder<DoctorQuestion> b)
    {
        b.ToTable("doctor_questions");
        b.HasKey(x => x.Id);
        b.Property(x => x.CareCircleId).IsRequired();
        b.Property(x => x.CreatedByUserId).IsRequired();
        b.Property(x => x.Question).IsRequired().HasMaxLength(2000);
        b.Property(x => x.Category).HasConversion<string>().HasMaxLength(40).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.AnswerNotes).HasMaxLength(4000);
        b.Property(x => x.CreatedAt).IsRequired();

        b.HasIndex(x => x.CareCircleId);
    }
}

public class SharedUpdateConfiguration : IEntityTypeConfiguration<SharedUpdate>
{
    public void Configure(EntityTypeBuilder<SharedUpdate> b)
    {
        b.ToTable("shared_updates");
        b.HasKey(x => x.Id);
        b.Property(x => x.CareCircleId).IsRequired();
        b.Property(x => x.CreatedByUserId).IsRequired();
        b.Property(x => x.Audience).HasConversion<string>().HasMaxLength(40).IsRequired();
        b.Property(x => x.Content).IsRequired().HasMaxLength(4000);
        b.Property(x => x.CreatedAt).IsRequired();

        b.HasIndex(x => x.CareCircleId);
    }
}
