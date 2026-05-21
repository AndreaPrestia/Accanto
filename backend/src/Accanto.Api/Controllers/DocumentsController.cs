using Accanto.Api.Common;
using Accanto.Application.Documents;
using Accanto.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Accanto.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/care-circles/{careCircleId:guid}/documents")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _svc;
    private readonly ICurrentUser _currentUser;

    public DocumentsController(IDocumentService svc, ICurrentUser currentUser)
    {
        _svc = svc;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> List(Guid careCircleId, CancellationToken ct)
        => Ok(await _svc.ListAsync(_currentUser.RequireUserId(), careCircleId, ct));

    [HttpGet("{documentId:guid}")]
    public async Task<ActionResult<DocumentDto>> Get(Guid careCircleId, Guid documentId, CancellationToken ct)
        => Ok(await _svc.GetAsync(_currentUser.RequireUserId(), careCircleId, documentId, ct));

    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<DocumentDto>> Upload(
        Guid careCircleId,
        [FromForm] IFormFile file,
        [FromForm] DocumentCategory category,
        [FromForm] string? notes,
        [FromForm] string? tags,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { title = "File mancante." });

        var tagList = string.IsNullOrWhiteSpace(tags)
            ? new List<string>()
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        await using var stream = file.OpenReadStream();
        var req = new UploadDocumentRequest(
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            category,
            notes,
            tagList
        );
        var dto = await _svc.UploadAsync(_currentUser.RequireUserId(), careCircleId, req, ct);
        return CreatedAtAction(nameof(Get), new { careCircleId, documentId = dto.Id }, dto);
    }

    [HttpGet("{documentId:guid}/download")]
    public async Task<IActionResult> Download(Guid careCircleId, Guid documentId, CancellationToken ct)
    {
        var d = await _svc.DownloadAsync(_currentUser.RequireUserId(), careCircleId, documentId, ct);
        return File(d.Content, d.ContentType, d.OriginalFileName);
    }

    [HttpDelete("{documentId:guid}")]
    public async Task<IActionResult> Delete(Guid careCircleId, Guid documentId, CancellationToken ct)
    {
        await _svc.DeleteAsync(_currentUser.RequireUserId(), careCircleId, documentId, ct);
        return NoContent();
    }
}
