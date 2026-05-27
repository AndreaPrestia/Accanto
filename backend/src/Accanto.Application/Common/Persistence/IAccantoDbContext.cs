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
    Microsoft.EntityFrameworkCore.DbSet<PushSubscription> PushSubscriptions { get; }
    Microsoft.EntityFrameworkCore.DbSet<AuditLogEntry> AuditLogEntries { get; }
    Microsoft.EntityFrameworkCore.DbSet<UserNotificationPreference> UserNotificationPreferences { get; }
    Microsoft.EntityFrameworkCore.DbSet<RefreshToken> RefreshTokens { get; }
    Microsoft.EntityFrameworkCore.DbSet<SecurityAuditLogEntry> SecurityAuditLogEntries { get; }
    Microsoft.EntityFrameworkCore.DbSet<CaregiverCheckIn> CaregiverCheckIns { get; }
    Microsoft.EntityFrameworkCore.DbSet<AiInteraction> AiInteractions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
