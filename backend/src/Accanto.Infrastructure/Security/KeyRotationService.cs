using Accanto.Application.Common.Storage;
using Accanto.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Infrastructure.Security;

/// <summary>
/// Riscrive tutti i dati cifrati (campi DB + file dei documenti) usando la chiave attiva
/// configurata in <see cref="EncryptionOptions"/>. Usato dalla CLI di rotazione.
/// </summary>
public sealed class KeyRotationService
{
    private readonly AccantoDbContext _db;
    private readonly IFileStorage _storage;

    public KeyRotationService(AccantoDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<KeyRotationReport> RotateAsync(CancellationToken cancellationToken = default)
    {
        var report = new KeyRotationReport();

        // Per ogni entita' con campi cifrati: caricare = decifrare (con il key-ring),
        // forzare lo stato Modified su tutte le proprieta' = riscrivere = cifrare con la chiave attiva.
        report.CareCircles = await ReencryptEntitySetAsync(_db.CareCircles, cancellationToken);
        report.TimelineEntries = await ReencryptEntitySetAsync(_db.TimelineEntries, cancellationToken);
        report.DoctorQuestions = await ReencryptEntitySetAsync(_db.DoctorQuestions, cancellationToken);
        report.SharedUpdates = await ReencryptEntitySetAsync(_db.SharedUpdates, cancellationToken);

        // I documenti hanno sia campi DB cifrati che il blob su filesystem.
        var docs = await _db.MedicalDocuments.ToListAsync(cancellationToken);
        foreach (var d in docs)
        {
            _db.Entry(d).State = EntityState.Modified;
        }
        if (docs.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);
        report.MedicalDocumentRows = docs.Count;

        foreach (var d in docs)
        {
            try
            {
                await _storage.RewriteWithActiveKeyAsync(d.StoragePath, cancellationToken);
                report.MedicalDocumentFiles++;
            }
            catch (FileNotFoundException)
            {
                report.MissingFiles.Add(d.StoragePath);
            }
        }

        return report;
    }

    private async Task<int> ReencryptEntitySetAsync<T>(DbSet<T> set, CancellationToken ct) where T : class
    {
        var items = await set.ToListAsync(ct);
        foreach (var it in items)
        {
            _db.Entry(it).State = EntityState.Modified;
        }
        if (items.Count > 0)
            await _db.SaveChangesAsync(ct);
        return items.Count;
    }
}

public sealed class KeyRotationReport
{
    public int CareCircles { get; set; }
    public int TimelineEntries { get; set; }
    public int DoctorQuestions { get; set; }
    public int SharedUpdates { get; set; }
    public int MedicalDocumentRows { get; set; }
    public int MedicalDocumentFiles { get; set; }
    public List<string> MissingFiles { get; } = new();
}
