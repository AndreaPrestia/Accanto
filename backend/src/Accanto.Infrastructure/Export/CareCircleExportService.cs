using System.Globalization;
using Accanto.Application.Common.Authorization;
using Accanto.Application.Common.Exceptions;
using Accanto.Application.Common.Persistence;
using Accanto.Application.Export;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Accanto.Infrastructure.Export;

public class CareCircleExportService : ICareCircleExportService
{
    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");

    private readonly IAccantoDbContext _db;
    private readonly ICareCircleAuthorization _auth;

    public CareCircleExportService(IAccantoDbContext db, ICareCircleAuthorization auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<CareCircleExportResult> ExportPdfAsync(
        Guid userId,
        Guid careCircleId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Viewer, cancellationToken);

        var circle = await _db.CareCircles.FirstOrDefaultAsync(c => c.Id == careCircleId, cancellationToken)
            ?? throw new NotFoundException("Cerchio non trovato.");

        var timelineQuery = _db.TimelineEntries
            .Where(e => e.CareCircleId == careCircleId)
            .Where(e => e.Visibility == TimelineVisibility.Circle || e.CreatedByUserId == userId);

        if (from.HasValue)
        {
            var f = from.Value;
            timelineQuery = timelineQuery.Where(e => e.OccurredAt >= f);
        }
        if (to.HasValue)
        {
            var t = to.Value;
            timelineQuery = timelineQuery.Where(e => e.OccurredAt <= t);
        }

        var timeline = await timelineQuery
            .OrderByDescending(e => e.OccurredAt)
            .ToListAsync(cancellationToken);

        var questions = await _db.DoctorQuestions
            .Where(q => q.CareCircleId == careCircleId)
            .Where(q => q.Status == DoctorQuestionStatus.ToAsk || q.Status == DoctorQuestionStatus.Asked)
            .OrderBy(q => q.Category)
            .ThenBy(q => q.CreatedAt)
            .ToListAsync(cancellationToken);

        var generatedAt = DateTimeOffset.Now;
        var bytes = Render(circle, timeline, questions, from, to, generatedAt);

        var safeName = MakeSafeFileName(circle.Name);
        var stamp = generatedAt.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var fileName = $"accanto-{safeName}-{stamp}.pdf";

        return new CareCircleExportResult(bytes, fileName);
    }

    private static byte[] Render(
        CareCircle circle,
        IReadOnlyList<TimelineEntry> timeline,
        IReadOnlyList<DoctorQuestion> questions,
        DateTimeOffset? from,
        DateTimeOffset? to,
        DateTimeOffset generatedAt)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(t => t.FontSize(11).FontColor(Colors.Grey.Darken4));

                page.Header().Column(col =>
                {
                    col.Item().Text(circle.Name).FontSize(18).SemiBold();
                    if (!string.IsNullOrWhiteSpace(circle.Description))
                    {
                        col.Item().Text(circle.Description!).FontSize(11).FontColor(Colors.Grey.Darken1);
                    }
                    col.Item().PaddingTop(4).Text(BuildSubtitle(from, to, generatedAt))
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(14);

                    col.Item().Element(SectionTitle("Diario"));
                    if (timeline.Count == 0)
                    {
                        col.Item().Text("Nessuna voce nel periodo selezionato.")
                            .Italic().FontColor(Colors.Grey.Darken1);
                    }
                    else
                    {
                        foreach (var entry in timeline)
                        {
                            col.Item().Element(c => TimelineRow(c, entry));
                        }
                    }

                    col.Item().Element(SectionTitle("Domande per il medico"));
                    if (questions.Count == 0)
                    {
                        col.Item().Text("Nessuna domanda aperta.")
                            .Italic().FontColor(Colors.Grey.Darken1);
                    }
                    else
                    {
                        foreach (var group in questions.GroupBy(q => q.Category))
                        {
                            col.Item().PaddingTop(4).Text(FormatCategory(group.Key))
                                .SemiBold().FontSize(11);
                            foreach (var q in group)
                            {
                                col.Item().Row(r =>
                                {
                                    r.ConstantItem(14).Text("\u2022");
                                    r.RelativeItem().Column(qc =>
                                    {
                                        qc.Item().Text(q.Question);
                                        if (!string.IsNullOrWhiteSpace(q.AnswerNotes))
                                        {
                                            qc.Item().PaddingLeft(4).Text(q.AnswerNotes!)
                                                .FontSize(10).FontColor(Colors.Grey.Darken2);
                                        }
                                    });
                                });
                            }
                        }
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Accanto \u00b7 ").FontSize(9).FontColor(Colors.Grey.Darken1);
                    t.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Darken1);
                    t.Span(" / ").FontSize(9).FontColor(Colors.Grey.Darken1);
                    t.TotalPages().FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            });
        });

        return doc.GeneratePdf();
    }

    private static Action<IContainer> SectionTitle(string title) => container =>
    {
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(4)
            .Text(title).FontSize(13).SemiBold().FontColor(Colors.Grey.Darken3);
    };

    private static void TimelineRow(IContainer container, TimelineEntry entry)
    {
        container.Border(0).PaddingVertical(2).Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(entry.Title).SemiBold();
                row.ConstantItem(120).AlignRight().Text(
                    entry.OccurredAt.ToLocalTime().ToString("dd MMM yyyy HH:mm", Italian))
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });

            col.Item().Text(FormatType(entry.Type)).FontSize(9).FontColor(Colors.Grey.Darken1);

            if (!string.IsNullOrWhiteSpace(entry.Content))
            {
                col.Item().PaddingTop(2).Text(entry.Content!).FontSize(10);
            }

            if (entry.Tags.Count > 0)
            {
                col.Item().PaddingTop(2).Text("Tag: " + string.Join(", ", entry.Tags))
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            }
        });
    }

    private static string BuildSubtitle(DateTimeOffset? from, DateTimeOffset? to, DateTimeOffset generatedAt)
    {
        var range = (from, to) switch
        {
            (null, null) => "Tutto il periodo",
            (DateTimeOffset f, null) => $"Dal {f.ToLocalTime():dd MMM yyyy}",
            (null, DateTimeOffset t) => $"Fino al {t.ToLocalTime():dd MMM yyyy}",
            (DateTimeOffset f, DateTimeOffset t) => $"Dal {f.ToLocalTime():dd MMM yyyy} al {t.ToLocalTime():dd MMM yyyy}"
        };
        return $"{range} \u00b7 Generato il {generatedAt.ToString("dd MMM yyyy HH:mm", Italian)}";
    }

    private static string FormatType(TimelineEntryType t) => t switch
    {
        TimelineEntryType.MedicalUpdate => "Aggiornamento medico",
        TimelineEntryType.Symptom => "Sintomo",
        TimelineEntryType.Medication => "Terapia",
        TimelineEntryType.Appointment => "Appuntamento",
        TimelineEntryType.Decision => "Decisione",
        TimelineEntryType.PersonalNote => "Nota personale",
        TimelineEntryType.Practical => "Pratico",
        _ => "Altro"
    };

    private static string FormatCategory(DoctorQuestionCategory c) => c switch
    {
        DoctorQuestionCategory.Diagnosis => "Diagnosi",
        DoctorQuestionCategory.Therapy => "Terapia",
        DoctorQuestionCategory.Pain => "Dolore",
        DoctorQuestionCategory.Nutrition => "Alimentazione",
        DoctorQuestionCategory.Hydration => "Idratazione",
        DoctorQuestionCategory.PalliativeCare => "Cure palliative",
        DoctorQuestionCategory.Discharge => "Dimissione",
        DoctorQuestionCategory.HomeCare => "Cura a domicilio",
        DoctorQuestionCategory.Emergency => "Emergenza",
        DoctorQuestionCategory.Prognosis => "Prognosi",
        DoctorQuestionCategory.Practical => "Pratico",
        _ => "Altro"
    };

    private static string MakeSafeFileName(string name)
    {
        var trimmed = name.Trim().ToLowerInvariant();
        var sb = new System.Text.StringBuilder();
        foreach (var ch in trimmed)
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (ch == ' ' || ch == '-' || ch == '_') sb.Append('-');
        }
        var result = sb.ToString().Trim('-');
        return string.IsNullOrEmpty(result) ? "cerchio" : result;
    }
}
