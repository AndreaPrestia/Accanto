using System.Text;
using Accanto.Application.CareCircles;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using Accanto.Infrastructure.Authorization;
using Accanto.Infrastructure.Export;
using FluentAssertions;

namespace Accanto.Tests;

public class CareCircleExportServiceTests
{
    private static (CareCircleExportService export, CareCircleService circles, Accanto.Infrastructure.Persistence.AccantoDbContext db) Build()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var db = TestDb.Create();
        var auth = new CareCircleAuthorization(db);
        var circles = new CareCircleService(db, auth, new NoOpAuditLog(), new NoOpOwnerTwoFactorOnboarding(), new CreateCareCircleRequestValidator(), new UpdateCareCircleRequestValidator());
        var export = new CareCircleExportService(db, auth);
        return (export, circles, db);
    }

    [Fact]
    public async Task ExportPdf_returns_pdf_bytes_with_filename()
    {
        var (export, circles, db) = Build();
        var owner = Guid.NewGuid();
        var circle = await circles.CreateAsync(owner, new CreateCareCircleRequest("Mamma Anna", "Cura a domicilio"));

        db.TimelineEntries.Add(new TimelineEntry
        {
            Id = Guid.NewGuid(),
            CareCircleId = circle.Id,
            CreatedByUserId = owner,
            OccurredAt = DateTimeOffset.UtcNow.AddDays(-1),
            Type = TimelineEntryType.MedicalUpdate,
            Title = "Visita cardiologica",
            Content = "Pressione 130/80.",
            Tags = new List<string> { "cardio" },
            Visibility = TimelineVisibility.Circle,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.DoctorQuestions.Add(new DoctorQuestion
        {
            Id = Guid.NewGuid(),
            CareCircleId = circle.Id,
            CreatedByUserId = owner,
            Question = "Si puo' ridurre il dosaggio?",
            Category = DoctorQuestionCategory.Therapy,
            Status = DoctorQuestionStatus.ToAsk,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await export.ExportPdfAsync(owner, circle.Id, null, null);

        result.Bytes.Length.Should().BeGreaterThan(500);
        Encoding.ASCII.GetString(result.Bytes, 0, 4).Should().Be("%PDF");
        result.FileName.Should().StartWith("accanto-mamma-anna-").And.EndWith(".pdf");
    }

    [Fact]
    public async Task ExportPdf_requires_membership()
    {
        var (export, circles, _) = Build();
        var owner = Guid.NewGuid();
        var outsider = Guid.NewGuid();
        var circle = await circles.CreateAsync(owner, new CreateCareCircleRequest("Papa'", null));

        var act = async () => await export.ExportPdfAsync(outsider, circle.Id, null, null);
        await act.Should().ThrowAsync<Accanto.Application.Common.Exceptions.ForbiddenException>();
    }
}
