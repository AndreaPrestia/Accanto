using System.IO.Compression;
using System.Text.Json;
using Accanto.Application.Account;
using Accanto.Application.CareCircles;
using Accanto.Application.Common.Storage;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using Accanto.Infrastructure.Security;
using FluentAssertions;

namespace Accanto.Tests;

public class GdprExportServiceTests
{
    private sealed class FakeStorage : IFileStorage
    {
        public Dictionary<string, byte[]> Files { get; } = new();
        public Task<StoredFile> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default)
        {
            if (!Files.TryGetValue(relativePath, out var bytes))
                throw new FileNotFoundException("missing", relativePath);
            return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
        }
        public Task DeleteAsync(string relativePath, CancellationToken ct = default) { Files.Remove(relativePath); return Task.CompletedTask; }
        public Task RewriteWithActiveKeyAsync(string relativePath, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static async Task<(Guid userId, Guid circleId, GdprExportService svc, FakeStorage storage, NoOpAuditLog audit)> SetupAsync()
    {
        var db = TestDb.Create();
        var hasher = new PasswordHasher();
        var auth = new Accanto.Infrastructure.Authorization.CareCircleAuthorization(db);
        var audit = new NoOpAuditLog();
        var circles = new CareCircleService(db, auth, audit, new CreateCareCircleRequestValidator(), new UpdateCareCircleRequestValidator());

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "u@test.local",
            DisplayName = "Utente Test",
            PasswordHash = hasher.Hash("password123"),
            Language = "it",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var circle = await circles.CreateAsync(user.Id, new CreateCareCircleRequest("Mamma", null));

        db.TimelineEntries.Add(new TimelineEntry
        {
            Id = Guid.NewGuid(),
            CareCircleId = circle.Id,
            CreatedByUserId = user.Id,
            Type = TimelineEntryType.PersonalNote,
            Title = "Voce",
            Content = "contenuto privato",
            Tags = new List<string> { "tag1" },
            OccurredAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.DoctorQuestions.Add(new DoctorQuestion
        {
            Id = Guid.NewGuid(),
            CareCircleId = circle.Id,
            CreatedByUserId = user.Id,
            Question = "Quando va il controllo?",
            Category = DoctorQuestionCategory.Other,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.SharedUpdates.Add(new SharedUpdate
        {
            Id = Guid.NewGuid(),
            CareCircleId = circle.Id,
            CreatedByUserId = user.Id,
            Audience = SharedUpdateAudience.CloseFamily,
            Content = "tutto ok",
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.UserNotificationPreferences.Add(new UserNotificationPreference
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Topic = NotificationTopic.TimelineEntryCreated,
            EmailEnabled = true,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var storage = new FakeStorage();
        var docId = Guid.NewGuid();
        storage.Files["blob/x.pdf"] = new byte[] { 1, 2, 3, 4, 5 };
        db.MedicalDocuments.Add(new MedicalDocument
        {
            Id = docId,
            CareCircleId = circle.Id,
            UploadedByUserId = user.Id,
            FileName = "x.pdf",
            OriginalFileName = "referto.pdf",
            ContentType = "application/pdf",
            SizeInBytes = 5,
            StoragePath = "blob/x.pdf",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = new GdprExportService(db, storage, audit);
        return (user.Id, circle.Id, svc, storage, audit);
    }

    [Fact]
    public async Task Export_produces_zip_with_profile_and_user_data()
    {
        var (userId, circleId, svc, _, audit) = await SetupAsync();

        var result = await svc.ExportAsync(userId);

        result.FileName.Should().StartWith("accanto-export-").And.EndWith(".zip");
        result.Content.Length.Should().BeGreaterThan(0);

        using var ms = new MemoryStream(result.Content);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

        var names = zip.Entries.Select(e => e.FullName).ToList();
        names.Should().Contain(new[]
        {
            "profile.json",
            "care-circles.json",
            "timeline.json",
            "documents.json",
            "doctor-questions.json",
            "shared-updates.json",
            "audit-log.json",
            "notification-preferences.json",
            "README.txt"
        });
        names.Should().Contain(n => n.StartsWith("documents/") && n.EndsWith("referto.pdf"));

        var profile = ReadJson(zip, "profile.json");
        profile.GetProperty("Email").GetString().Should().Be("u@test.local");
        profile.GetProperty("Language").GetString().Should().Be("it");

        var timeline = ReadJson(zip, "timeline.json");
        timeline.GetArrayLength().Should().Be(1);
        timeline[0].GetProperty("Content").GetString().Should().Be("contenuto privato");

        audit.Calls.Should().Contain(c => c.ActionType == AuditActionType.DataExported && c.CareCircleId == circleId);
    }

    [Fact]
    public async Task Export_includes_document_file_bytes_when_present()
    {
        var (userId, _, svc, _, _) = await SetupAsync();

        var result = await svc.ExportAsync(userId);

        using var ms = new MemoryStream(result.Content);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        var docEntry = zip.Entries.First(e => e.FullName.StartsWith("documents/"));
        using var s = docEntry.Open();
        using var copy = new MemoryStream();
        await s.CopyToAsync(copy);
        copy.ToArray().Should().BeEquivalentTo(new byte[] { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public async Task Export_skips_missing_document_files_without_failing()
    {
        var (userId, _, svc, storage, _) = await SetupAsync();
        storage.Files.Clear();

        var result = await svc.ExportAsync(userId);

        using var ms = new MemoryStream(result.Content);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        zip.Entries.Should().NotContain(e => e.FullName.StartsWith("documents/"));
        // Il metadato resta comunque presente in documents.json
        var docs = ReadJson(zip, "documents.json");
        docs.GetArrayLength().Should().Be(1);
    }

    private static JsonElement ReadJson(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path)!;
        using var s = entry.Open();
        using var doc = JsonDocument.Parse(s);
        return doc.RootElement.Clone();
    }
}
