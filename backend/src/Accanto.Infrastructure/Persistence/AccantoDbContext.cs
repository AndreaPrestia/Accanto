using Accanto.Application.Common.Persistence;
using Accanto.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Infrastructure.Persistence;

public class AccantoDbContext : DbContext, IAccantoDbContext
{
    public AccantoDbContext(DbContextOptions<AccantoDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<CareCircle> CareCircles => Set<CareCircle>();
    public DbSet<CareCircleMember> CareCircleMembers => Set<CareCircleMember>();
    public DbSet<TimelineEntry> TimelineEntries => Set<TimelineEntry>();
    public DbSet<MedicalDocument> MedicalDocuments => Set<MedicalDocument>();
    public DbSet<DoctorQuestion> DoctorQuestions => Set<DoctorQuestion>();
    public DbSet<SharedUpdate> SharedUpdates => Set<SharedUpdate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccantoDbContext).Assembly);
    }
}
