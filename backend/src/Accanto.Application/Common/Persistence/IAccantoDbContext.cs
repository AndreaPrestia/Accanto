using Accanto.Domain.Entities;

namespace Accanto.Application.Common.Persistence;

public interface IAccantoDbContext
{
    Microsoft.EntityFrameworkCore.DbSet<User> Users { get; }
    Microsoft.EntityFrameworkCore.DbSet<CareCircle> CareCircles { get; }
    Microsoft.EntityFrameworkCore.DbSet<CareCircleMember> CareCircleMembers { get; }
    Microsoft.EntityFrameworkCore.DbSet<CareCircleInvite> CareCircleInvites { get; }
    Microsoft.EntityFrameworkCore.DbSet<TimelineEntry> TimelineEntries { get; }
    Microsoft.EntityFrameworkCore.DbSet<MedicalDocument> MedicalDocuments { get; }
    Microsoft.EntityFrameworkCore.DbSet<DoctorQuestion> DoctorQuestions { get; }
    Microsoft.EntityFrameworkCore.DbSet<SharedUpdate> SharedUpdates { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
