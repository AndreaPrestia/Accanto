using Accanto.Application.Common.Exceptions;
using Accanto.Application.CareCircles;
using Accanto.Application.Documents;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using Accanto.Infrastructure.Authorization;
using Accanto.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Accanto.Tests;

public class DocumentServiceUploadGuardsTests : IDisposable
{
    private readonly string _root;

    public DocumentServiceUploadGuardsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "accanto-doc-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private async Task<(DocumentService svc, Guid ownerId, Guid circleId)> BuildAsync(IMalwareScanner? scanner = null)
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
            scanner ?? new NoopMalwareScanner(),
            Options.Create(new DocumentStorageOptions()));

        return (svc, ownerId, circle.Id);
    }

    [Fact]
    public async Task Upload_rejects_content_type_spoofing()
    {
        var (svc, ownerId, circleId) = await BuildAsync();
        // PNG bytes ma il client dichiara application/pdf.
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 };
        using var ms = new MemoryStream(png);
        var req = new UploadDocumentRequest(
            ms, "fake.pdf", "application/pdf", png.Length,
            DocumentCategory.Other, null, new List<string>());

        var act = async () => await svc.UploadAsync(ownerId, circleId, req);
        await act.Should().ThrowAsync<AppValidationException>()
            .Where(e => e.Message.Contains("non corrisponde", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Upload_accepts_valid_pdf()
    {
        var (svc, ownerId, circleId) = await BuildAsync();
        // PDF strutturalmente valido: header + body + marker %%EOF in coda.
        var pdf = "%PDF-1.7\n%body\n%%EOF\n"u8.ToArray();
        using var ms = new MemoryStream(pdf);
        var req = new UploadDocumentRequest(
            ms, "ok.pdf", "application/pdf", pdf.Length,
            DocumentCategory.Other, null, new List<string>());

        var dto = await svc.UploadAsync(ownerId, circleId, req);
        dto.OriginalFileName.Should().Be("ok.pdf");
        dto.ContentType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task Upload_rejects_when_scanner_flags_malware()
    {
        var (svc, ownerId, circleId) = await BuildAsync(new AlwaysMalwareScanner());
        var pdf = "%PDF-1.7\n%%EOF\n"u8.ToArray();
        using var ms = new MemoryStream(pdf);
        var req = new UploadDocumentRequest(
            ms, "evil.pdf", "application/pdf", pdf.Length,
            DocumentCategory.Other, null, new List<string>());

        var act = async () => await svc.UploadAsync(ownerId, circleId, req);
        await act.Should().ThrowAsync<AppValidationException>()
            .Where(e => e.Message.Contains("antivirus", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class AlwaysMalwareScanner : IMalwareScanner
    {
        public Task ScanAsync(Stream content, string originalFileName, CancellationToken cancellationToken = default)
            => throw new MalwareDetectedException("Test.EICAR");
    }
}
