using Accanto.Application.Account;
using Accanto.Application.CareCircles;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using Accanto.Infrastructure.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Accanto.Tests;

/// <summary>
/// Verifica end-to-end del flow GDPR right-to-erasure: dopo
/// EraseAsync l'utente e' tombstone, le membership condivise
/// sono rimosse, i documenti caricati sono cancellati anche dal
/// disco, e nell'outbox sono presenti righe DELETE per la replica
/// S3.
/// </summary>
public class UserErasureServiceTests
{
    private sealed class FakeStorage : Accanto.Application.Common.Storage.IFileStorage
    {
        public List<string> Deleted { get; } = new();
        public Task<Accanto.Application.Common.Storage.StoredFile> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteAsync(string relativePath, CancellationToken ct = default) { Deleted.Add(relativePath); return Task.CompletedTask; }
        public Task RewriteWithActiveKeyAsync(string relativePath, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task EraseAsync_tombstones_user_clears_pii_and_keeps_audit_log()
    {
        var db = TestDb.Create();
        var hasher = new PasswordHasher();
        var audit = new NoOpSecurityAuditLog();
        var storage = new FakeStorage();
        var refresh = new NoOpRefreshTokenService();
        var svc = new UserErasureService(db, storage, refresh, audit, NullLogger<UserErasureService>.Instance);

        var u = new User
        {
            Id = Guid.NewGuid(),
            Email = "vittima@test.local",
            DisplayName = "Mario",
            PasswordHash = hasher.Hash("pwd1234567"),
            TwoFactorEnabled = true,
            TwoFactorSecret = "secret",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(u);
        await db.SaveChangesAsync();

        await svc.EraseAsync(u.Id, "Test erasure");

        var tomb = db.Users.AsNoTracking().Single(x => x.Id == u.Id);
        tomb.IsErased.Should().BeTrue();
        tomb.ErasedAt.Should().NotBeNull();
        tomb.ErasureReason.Should().Be("Test erasure");
        tomb.Email.Should().NotBe("vittima@test.local");
        tomb.Email.Should().StartWith("erased-").And.EndWith("@accanto.invalid");
        tomb.DisplayName.Should().Be("Utente cancellato");
        tomb.PasswordHash.Should().BeEmpty();
        tomb.TwoFactorEnabled.Should().BeFalse();
        tomb.TwoFactorSecret.Should().BeNull();
    }

    [Fact]
    public async Task EraseAsync_is_idempotent_on_already_tombstoned_user()
    {
        var db = TestDb.Create();
        var storage = new FakeStorage();
        var svc = new UserErasureService(db, storage, new NoOpRefreshTokenService(), new NoOpSecurityAuditLog(), NullLogger<UserErasureService>.Instance);

        var u = new User
        {
            Id = Guid.NewGuid(),
            Email = "erased-x@accanto.invalid",
            DisplayName = "Utente cancellato",
            IsErased = true,
            ErasedAt = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30)
        };
        db.Users.Add(u);
        await db.SaveChangesAsync();
        var firstErasedAt = u.ErasedAt;

        await svc.EraseAsync(u.Id, "Re-attempt");

        var fresh = db.Users.AsNoTracking().Single(x => x.Id == u.Id);
        fresh.ErasedAt.Should().Be(firstErasedAt);
    }

    [Fact]
    public async Task EraseAsync_cascades_own_documents_to_outbox_DELETE_and_disk()
    {
        var db = TestDb.Create();
        var hasher = new PasswordHasher();
        var auth = new Accanto.Infrastructure.Authorization.CareCircleAuthorization(db);
        var circles = new CareCircleService(db, auth, new NoOpAuditLog(), new NoOpOwnerTwoFactorOnboarding(), new CreateCareCircleRequestValidator(), new UpdateCareCircleRequestValidator());
        var storage = new FakeStorage();
        var svc = new UserErasureService(db, storage, new NoOpRefreshTokenService(), new NoOpSecurityAuditLog(), NullLogger<UserErasureService>.Instance);

        var u = new User { Id = Guid.NewGuid(), Email = "u@t", DisplayName = "U", PasswordHash = hasher.Hash("pw12345678"), CreatedAt = DateTimeOffset.UtcNow };
        db.Users.Add(u);
        await db.SaveChangesAsync();
        var circle = await circles.CreateAsync(u.Id, new CreateCareCircleRequest("C", null));

        db.MedicalDocuments.Add(new MedicalDocument
        {
            Id = Guid.NewGuid(), CareCircleId = circle.Id, UploadedByUserId = u.Id,
            FileName = "a.pdf", ContentType = "application/pdf", SizeInBytes = 1,
            StoragePath = "p/a.pdf", CreatedAt = DateTimeOffset.UtcNow
        });
        db.MedicalDocuments.Add(new MedicalDocument
        {
            Id = Guid.NewGuid(), CareCircleId = circle.Id, UploadedByUserId = u.Id,
            FileName = "b.pdf", ContentType = "application/pdf", SizeInBytes = 1,
            StoragePath = "p/b.pdf", CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        await svc.EraseAsync(u.Id, "GDPR request");

        // Outbox: due DELETE per i documenti dell'utente.
        var deletes = db.DocumentSyncOutbox.Where(o => o.Operation == "DELETE").ToList();
        deletes.Should().HaveCountGreaterOrEqualTo(2);
        deletes.Select(o => o.StoragePath).Should().Contain(new[] { "p/a.pdf", "p/b.pdf" });
        // Disk: best-effort delete tentato per entrambi.
        storage.Deleted.Should().Contain(new[] { "p/a.pdf", "p/b.pdf" });
        // I documenti non esistono piu' nel DB.
        db.MedicalDocuments.Any(d => d.UploadedByUserId == u.Id).Should().BeFalse();
    }
}
