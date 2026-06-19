using Accanto.Application.CareCircles;
using Accanto.Application.Documents;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using Accanto.Infrastructure.Authorization;
using Accanto.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Accanto.Tests;

/// <summary>
/// Verifica che ogni upload/delete inserisca una riga nella
/// document_sync_outbox in modo coerente con la transazione del DB.
/// Il worker S3 e' opt-in e qui non e' istanziato: testiamo solo
/// l'enqueue, non la replica vera.
/// </summary>
public class DocumentSyncOutboxTests : IDisposable
{
    private readonly string _root;

    public DocumentSyncOutboxTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "accanto-outbox-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public async Task Upload_enqueues_pending_PUT_row()
    {
        var (svc, db, ownerId, circleId) = await BuildAsync();

        var pdf = "%PDF-1.7\n%body\n%%EOF\n"u8.ToArray();
        using var ms = new MemoryStream(pdf);
        var dto = await svc.UploadAsync(ownerId, circleId,
            new UploadDocumentRequest(ms, "ok.pdf", "application/pdf", pdf.Length,
                DocumentCategory.Other, null, new List<string>()));

        var rows = await db.DocumentSyncOutbox.AsNoTracking().ToListAsync();
        rows.Should().HaveCount(1);
        var row = rows[0];
        row.Operation.Should().Be("PUT");
        row.Status.Should().Be("pending");
        row.RetryCount.Should().Be(0);
        row.DocumentId.Should().Be(dto.Id);
        row.StoragePath.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Delete_enqueues_pending_DELETE_row_and_keeps_PUT()
    {
        var (svc, db, ownerId, circleId) = await BuildAsync();

        var pdf = "%PDF-1.7\n%body\n%%EOF\n"u8.ToArray();
        using var ms = new MemoryStream(pdf);
        var dto = await svc.UploadAsync(ownerId, circleId,
            new UploadDocumentRequest(ms, "ok.pdf", "application/pdf", pdf.Length,
                DocumentCategory.Other, null, new List<string>()));

        await svc.DeleteAsync(ownerId, circleId, dto.Id);

        var rows = await db.DocumentSyncOutbox.AsNoTracking()
            .OrderBy(r => r.CreatedAt).ToListAsync();
        rows.Should().HaveCount(2);
        rows[0].Operation.Should().Be("PUT");
        rows[1].Operation.Should().Be("DELETE");
        rows[1].Status.Should().Be("pending");
        rows[1].StoragePath.Should().Be(rows[0].StoragePath);
    }

    private async Task<(DocumentService svc, AccantoDbContextLike db, Guid ownerId, Guid circleId)> BuildAsync()
    {
        var db = TestDb.Create();
        var auth = new CareCircleAuthorization(db);
        var storage = new LocalFileStorage(
            Options.Create(new StorageOptions { RootPath = _root, MaxFileSizeBytes = 1_000_000 }),
            new NullFieldProtector());

        var circles = new CareCircleService(
            db, auth, new NoOpAuditLog(),
            new NoOpOwnerTwoFactorOnboarding(),
            new CreateCareCircleRequestValidator(),
            new UpdateCareCircleRequestValidator());

        var ownerId = Guid.NewGuid();
        var circle = await circles.CreateAsync(ownerId, new CreateCareCircleRequest("Mamma", null));

        var svc = new DocumentService(
            db, auth, storage, new NoOpAuditLog(),
            new NoopMalwareScanner(),
            Options.Create(new DocumentStorageOptions()));

        return (svc, new AccantoDbContextLike(db), ownerId, circle.Id);
    }

    /// <summary>
    /// Wrapper sottile che riespone l'unico DbSet di interesse senza
    /// rendere pubblico l'intero contesto al test.
    /// </summary>
    private sealed class AccantoDbContextLike
    {
        private readonly Accanto.Infrastructure.Persistence.AccantoDbContext _inner;
        public AccantoDbContextLike(Accanto.Infrastructure.Persistence.AccantoDbContext inner) => _inner = inner;
        public DbSet<DocumentSyncOutboxEntry> DocumentSyncOutbox => _inner.DocumentSyncOutbox;
    }
}
