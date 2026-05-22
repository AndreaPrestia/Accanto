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
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<DocumentDto>> Upload(
        Guid careCircleId,
        [FromForm] UploadDocumentForm form,
        CancellationToken ct)
    {
        if (form.File is null || form.File.Length == 0)
            return BadRequest(new { title = "File mancante." });

        var tagList = string.IsNullOrWhiteSpace(form.Tags)
            ? new List<string>()
            : form.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        await using var stream = form.File.OpenReadStream();
        var req = new UploadDocumentRequest(
            stream,
            form.File.FileName,
            form.File.ContentType,
            form.File.Length,
            form.Category,
            form.Notes,
            tagList
        );
        var dto = await _svc.UploadAsync(_currentUser.RequireUserId(), careCircleId, req, ct);
        return CreatedAtAction(nameof(Get), new { careCircleId, documentId = dto.Id }, dto);
    }

    public class UploadDocumentForm
    {
        public IFormFile? File { get; set; }
        public DocumentCategory Category { get; set; }
        public string? Notes { get; set; }
        public string? Tags { get; set; }
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
