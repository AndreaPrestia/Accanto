using Accanto.Application.Audit;
using Accanto.Application.Common.Authorization;
using Accanto.Application.Common.Exceptions;
using Accanto.Application.Common.Persistence;
using Accanto.Application.Common.Storage;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Accanto.Application.Documents;

public class DocumentService : IDocumentService
{
    private readonly IAccantoDbContext _db;
    private readonly ICareCircleAuthorization _auth;
    private readonly IFileStorage _storage;
    private readonly IAuditLog _audit;
    private readonly IMalwareScanner _malwareScanner;
    private readonly DocumentStorageOptions _options;

    public DocumentService(
        IAccantoDbContext db,
        ICareCircleAuthorization auth,
        IFileStorage storage,
        IAuditLog audit,
        IMalwareScanner malwareScanner,
        IOptions<DocumentStorageOptions> options)
    {
        _db = db;
        _auth = auth;
        _storage = storage;
        _audit = audit;
        _malwareScanner = malwareScanner;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<DocumentDto>> ListAsync(Guid userId, Guid careCircleId, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Viewer, cancellationToken);

        var rows = await _db.MedicalDocuments
            .Where(d => d.CareCircleId == careCircleId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    public async Task<DocumentDto> GetAsync(Guid userId, Guid careCircleId, Guid documentId, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Viewer, cancellationToken);

        var doc = await _db.MedicalDocuments.FirstOrDefaultAsync(d => d.Id == documentId && d.CareCircleId == careCircleId, cancellationToken)
            ?? throw new NotFoundException("Documento non trovato.");
        return Map(doc);
    }

    public async Task<DocumentDto> UploadAsync(Guid userId, Guid careCircleId, UploadDocumentRequest request, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Caregiver, cancellationToken);

        if (request.SizeInBytes <= 0)
        {
            throw new AppValidationException("Il file è vuoto.");
        }
        if (request.SizeInBytes > _options.MaxFileSizeBytes)
        {
            throw new AppValidationException($"Il file supera la dimensione massima ({_options.MaxFileSizeBytes / (1024 * 1024)} MB).");
        }
        var contentType = (request.ContentType ?? string.Empty).Trim().ToLowerInvariant();
        if (!_options.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new AppValidationException("Tipo di file non consentito.");
        }
        if (string.IsNullOrWhiteSpace(request.OriginalFileName))
        {
            throw new AppValidationException("Nome file mancante.");
        }

        // Difesa contro file polyglot / content-type spoofed dal client:
        // bufferizziamo l'intero stream (size gia' validata <= MaxFileSizeBytes
        // poco sopra) e ispezioniamo i primi byte per verificare che la
        // firma matchi il content-type dichiarato.
        var buffer = new MemoryStream((int)request.SizeInBytes);
        await request.Content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var headLen = (int)Math.Min(FileSignatureValidator.InspectBytes, buffer.Length);
        if (headLen <= 0 || !FileSignatureValidator.IsValid(buffer.GetBuffer().AsSpan(0, headLen), contentType))
        {
            throw new AppValidationException("Il contenuto del file non corrisponde al tipo dichiarato.");
        }

        // Anti-malware: noop di default, ClamAV se configurato.
        // MalwareDetectedException si propaga al middleware → 422 con motivo.
        try
        {
            await _malwareScanner.ScanAsync(buffer, request.OriginalFileName, cancellationToken);
        }
        catch (MalwareDetectedException ex)
        {
            throw new AppValidationException($"File rifiutato dall'antivirus: {ex.Signature}");
        }
        buffer.Position = 0;

        var stored = await _storage.SaveAsync(buffer, request.OriginalFileName, contentType, cancellationToken);

        var doc = new MedicalDocument
        {
            Id = Guid.NewGuid(),
            CareCircleId = careCircleId,
            UploadedByUserId = userId,
            FileName = stored.InternalFileName,
            OriginalFileName = SanitizeOriginalName(request.OriginalFileName),
            ContentType = contentType,
            SizeInBytes = stored.SizeInBytes,
            Category = request.Category,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Tags = NormalizeTags(request.Tags),
            StoragePath = stored.RelativePath,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.MedicalDocuments.Add(doc);
        await _db.SaveChangesAsync(cancellationToken);

        _ = _audit.LogAsync(careCircleId, userId, AuditActionType.DocumentUploaded, AuditResourceType.MedicalDocument, doc.Id, doc.OriginalFileName, CancellationToken.None);

        return Map(doc);
    }

    public async Task<DocumentDownload> DownloadAsync(Guid userId, Guid careCircleId, Guid documentId, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Viewer, cancellationToken);

        var doc = await _db.MedicalDocuments.FirstOrDefaultAsync(d => d.Id == documentId && d.CareCircleId == careCircleId, cancellationToken)
            ?? throw new NotFoundException("Documento non trovato.");

        var stream = await _storage.OpenReadAsync(doc.StoragePath, cancellationToken);
        return new DocumentDownload(stream, doc.ContentType, doc.OriginalFileName, doc.SizeInBytes);
    }

    public async Task DeleteAsync(Guid userId, Guid careCircleId, Guid documentId, CancellationToken cancellationToken = default)
    {
        await _auth.EnsureMemberAsync(userId, careCircleId, CareCircleRole.Caregiver, cancellationToken);

        var doc = await _db.MedicalDocuments.FirstOrDefaultAsync(d => d.Id == documentId && d.CareCircleId == careCircleId, cancellationToken)
            ?? throw new NotFoundException("Documento non trovato.");

        var name = doc.OriginalFileName;
        _db.MedicalDocuments.Remove(doc);
        await _db.SaveChangesAsync(cancellationToken);

        _ = _audit.LogAsync(careCircleId, userId, AuditActionType.DocumentDeleted, AuditResourceType.MedicalDocument, documentId, name, CancellationToken.None);

        try
        {
            await _storage.DeleteAsync(doc.StoragePath, cancellationToken);
        }
        catch
        {
            // best effort: file may already be missing
        }
    }

    private static string SanitizeOriginalName(string name)
    {
        var trimmed = Path.GetFileName(name).Trim();
        if (string.IsNullOrEmpty(trimmed)) return "file";
        return trimmed.Length > 200 ? trimmed[..200] : trimmed;
    }

    private static List<string> NormalizeTags(IEnumerable<string>? tags) =>
        (tags ?? Enumerable.Empty<string>())
            .Select(t => t?.Trim() ?? string.Empty)
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static DocumentDto Map(MedicalDocument d) => new(
        d.Id, d.CareCircleId, d.UploadedByUserId, d.OriginalFileName, d.ContentType, d.SizeInBytes,
        d.Category, d.Notes, d.Tags, d.CreatedAt);
}
