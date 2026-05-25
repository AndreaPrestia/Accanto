using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Accanto.Application.Audit;
using Accanto.Application.Common.Exceptions;
using Accanto.Application.Common.Persistence;
using Accanto.Application.Common.Storage;
using Accanto.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Application.Account;

/// <summary>
/// Esporta in un singolo archivio ZIP tutti i dati personali dell'utente: profilo,
/// preferenze, voci di diario / domande / aggiornamenti / documenti caricati,
/// audit log delle proprie azioni e metadati dei cerchi a cui partecipa. I documenti
/// vengono inclusi in chiaro nella cartella "documents/".
/// </summary>
public class GdprExportService : IGdprExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IAccantoDbContext _db;
    private readonly IFileStorage _storage;
    private readonly IAuditLog _audit;

    public GdprExportService(IAccantoDbContext db, IFileStorage storage, IAuditLog audit)
    {
        _db = db;
        _storage = storage;
        _audit = audit;
    }

    public async Task<GdprExportResult> ExportAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        var memberCircleIds = await _db.CareCircleMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.CareCircleId)
            .ToListAsync(cancellationToken);

        var circles = await _db.CareCircles
            .Where(c => memberCircleIds.Contains(c.Id))
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        var timeline = await _db.TimelineEntries
            .Where(t => t.CreatedByUserId == userId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        var documents = await _db.MedicalDocuments
            .Where(d => d.UploadedByUserId == userId)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        var questions = await _db.DoctorQuestions
            .Where(q => q.CreatedByUserId == userId)
            .OrderBy(q => q.CreatedAt)
            .ToListAsync(cancellationToken);

        var updates = await _db.SharedUpdates
            .Where(s => s.CreatedByUserId == userId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        var auditEntries = await _db.AuditLogEntries
            .Where(a => a.PerformedByUserId == userId)
            .OrderBy(a => a.Timestamp)
            .ToListAsync(cancellationToken);

        var prefs = await _db.UserNotificationPreferences
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.Topic)
            .ToListAsync(cancellationToken);

        var checkIns = await _db.CaregiverCheckIns
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        var generatedAt = DateTimeOffset.UtcNow;

        var profilePayload = new
        {
            user.Id,
            user.Email,
            user.DisplayName,
            user.Language,
            user.CreatedAt,
            ExportedAt = generatedAt
        };

        var circlesPayload = circles.Select(c => new
        {
            c.Id,
            c.Name,
            c.Description,
            Status = c.Status.ToString(),
            c.CreatedAt,
            c.UpdatedAt
        });

        var timelinePayload = timeline.Select(t => new
        {
            t.Id,
            t.CareCircleId,
            t.OccurredAt,
            Type = t.Type.ToString(),
            t.Title,
            t.Content,
            t.Tags,
            Visibility = t.Visibility.ToString(),
            t.CreatedAt,
            t.UpdatedAt
        });

        var documentsPayload = documents.Select(d => new
        {
            d.Id,
            d.CareCircleId,
            d.OriginalFileName,
            d.ContentType,
            d.SizeInBytes,
            Category = d.Category.ToString(),
            d.Notes,
            d.Tags,
            d.CreatedAt,
            ArchivePath = $"documents/{d.Id}-{SanitizeFileName(d.OriginalFileName)}"
        });

        var questionsPayload = questions.Select(q => new
        {
            q.Id,
            q.CareCircleId,
            q.Question,
            Category = q.Category.ToString(),
            Status = q.Status.ToString(),
            q.AnswerNotes,
            q.CreatedAt,
            q.UpdatedAt
        });

        var updatesPayload = updates.Select(s => new
        {
            s.Id,
            s.CareCircleId,
            Audience = s.Audience.ToString(),
            s.Content,
            s.CreatedAt
        });

        var auditPayload = auditEntries.Select(a => new
        {
            a.Id,
            a.CareCircleId,
            ActionType = a.ActionType.ToString(),
            ResourceType = a.ResourceType.ToString(),
            a.ResourceId,
            a.Summary,
            a.Timestamp
        });

        var prefsPayload = prefs.Select(p => new
        {
            Topic = p.Topic.ToString(),
            p.EmailEnabled,
            p.UpdatedAt
        });

        var checkInsPayload = checkIns.Select(c => new
        {
            c.Id,
            c.Mood,
            c.Energy,
            c.Stress,
            c.Note,
            c.CreatedAt
        });

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            await WriteJsonEntryAsync(zip, "profile.json", profilePayload, cancellationToken);
            await WriteJsonEntryAsync(zip, "care-circles.json", circlesPayload, cancellationToken);
            await WriteJsonEntryAsync(zip, "timeline.json", timelinePayload, cancellationToken);
            await WriteJsonEntryAsync(zip, "documents.json", documentsPayload, cancellationToken);
            await WriteJsonEntryAsync(zip, "doctor-questions.json", questionsPayload, cancellationToken);
            await WriteJsonEntryAsync(zip, "shared-updates.json", updatesPayload, cancellationToken);
            await WriteJsonEntryAsync(zip, "audit-log.json", auditPayload, cancellationToken);
            await WriteJsonEntryAsync(zip, "notification-preferences.json", prefsPayload, cancellationToken);
            await WriteJsonEntryAsync(zip, "wellbeing-check-ins.json", checkInsPayload, cancellationToken);
            await WriteTextEntryAsync(zip, "README.txt", BuildReadme(user.DisplayName, generatedAt), cancellationToken);

            foreach (var doc in documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await using var src = await _storage.OpenReadAsync(doc.StoragePath, cancellationToken);
                    var entry = zip.CreateEntry($"documents/{doc.Id}-{SanitizeFileName(doc.OriginalFileName)}", CompressionLevel.Optimal);
                    await using var dst = entry.Open();
                    await src.CopyToAsync(dst, cancellationToken);
                }
                catch (FileNotFoundException)
                {
                    // File mancante sul filesystem: lasciamo solo il metadato in documents.json.
                }
            }
        }

        var bytes = buffer.ToArray();
        var fileName = $"accanto-export-{generatedAt:yyyyMMdd-HHmmss}.zip";

        // Audit: una entry per cerchio dell'utente, così la traccia è visibile a tutti i membri.
        foreach (var circleId in memberCircleIds)
        {
            await _audit.LogAsync(
                circleId,
                userId,
                AuditActionType.DataExported,
                AuditResourceType.CareCircle,
                circleId,
                "Esportazione dati GDPR",
                cancellationToken);
        }

        return new GdprExportResult(fileName, bytes);
    }

    private static async Task WriteJsonEntryAsync<T>(ZipArchive zip, string path, T payload, CancellationToken ct)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, payload, JsonOptions, ct);
    }

    private static async Task WriteTextEntryAsync(ZipArchive zip, string path, string content, CancellationToken ct)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        await stream.WriteAsync(bytes, ct);
    }

    private static string BuildReadme(string displayName, DateTimeOffset generatedAt)
    {
        return
$@"Esportazione dati Accanto
==========================

Intestatario: {displayName}
Generata il : {generatedAt:O}

Contenuto:
- profile.json                 Profilo utente e lingua preferita.
- care-circles.json            Cerchi di cura di cui sei membro.
- timeline.json                Voci di diario che hai creato.
- documents.json               Metadati dei documenti che hai caricato.
- documents/                   File originali in chiaro (corrispondenti a documents.json).
- doctor-questions.json        Domande per il medico che hai creato.
- shared-updates.json          Aggiornamenti condivisi che hai pubblicato.
- audit-log.json               Tracce delle azioni che hai compiuto.
- notification-preferences.json Preferenze per le notifiche email.
- wellbeing-check-ins.json     Check-in del tuo benessere (privati).
";
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "file";
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        }
        var clean = sb.ToString().Trim();
        return clean.Length == 0 ? "file" : clean;
    }
}
