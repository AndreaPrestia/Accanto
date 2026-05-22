using Accanto.Application.Common.Persistence;
using Accanto.Application.Common.Security;
using Accanto.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Accanto.Infrastructure.Persistence;

public class AccantoDbContext : DbContext, IAccantoDbContext
{
    private readonly IFieldProtector _protector;

    public AccantoDbContext(DbContextOptions<AccantoDbContext> options, IFieldProtector protector) : base(options)
    {
        _protector = protector;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<CareCircle> CareCircles => Set<CareCircle>();
    public DbSet<CareCircleMember> CareCircleMembers => Set<CareCircleMember>();
    public DbSet<CareCircleInvite> CareCircleInvites => Set<CareCircleInvite>();
    public DbSet<TimelineEntry> TimelineEntries => Set<TimelineEntry>();
    public DbSet<MedicalDocument> MedicalDocuments => Set<MedicalDocument>();
    public DbSet<DoctorQuestion> DoctorQuestions => Set<DoctorQuestion>();
    public DbSet<SharedUpdate> SharedUpdates => Set<SharedUpdate>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccantoDbContext).Assembly);

        // Cifratura a riposo: AES-GCM su campi sensibili (Tags lasciati in chiaro per query LINQ).
        var protector = _protector;
        var converter = new ValueConverter<string, string>(
            v => protector.Encrypt(v),
            v => protector.Decrypt(v));

        // EF gestisce null automaticamente: il converter NON viene invocato su valori null.
        var comparer = new ValueComparer<string>(
            (a, b) => a == b,
            v => v == null ? 0 : v.GetHashCode(),
            v => v);

        modelBuilder.Entity<CareCircle>().Property(x => x.Description).HasConversion(converter!, comparer);
        modelBuilder.Entity<TimelineEntry>().Property(x => x.Title).HasConversion(converter, comparer);
        modelBuilder.Entity<TimelineEntry>().Property(x => x.Content).HasConversion(converter, comparer);
        modelBuilder.Entity<MedicalDocument>().Property(x => x.OriginalFileName).HasConversion(converter, comparer);
        modelBuilder.Entity<MedicalDocument>().Property(x => x.Notes).HasConversion(converter!, comparer);
        modelBuilder.Entity<DoctorQuestion>().Property(x => x.Question).HasConversion(converter, comparer);
        modelBuilder.Entity<DoctorQuestion>().Property(x => x.AnswerNotes).HasConversion(converter!, comparer);
        modelBuilder.Entity<SharedUpdate>().Property(x => x.Content).HasConversion(converter, comparer);
    }
}
