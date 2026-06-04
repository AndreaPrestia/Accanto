using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Accanto.Api.Controllers;

/// <summary>
/// Endpoint receiver per le violazioni CSP (e in prospettiva altri report
/// del Reporting API del browser). Idempotente, anonimo, rate-limitato.
/// </summary>
/// <remarks>
/// Accetta sia il formato legacy <c>application/csp-report</c>
/// ({"csp-report": {...}}) sia il formato moderno
/// <c>application/reports+json</c> ([{type, body}, ...]). Logga gli
/// elementi diagnostici a INFO con categoria <c>Accanto.Security.Csp</c>:
/// l'aggregazione (es. via Seq/Loki) e' delegata all'osservabilita'.
///
/// Nessuna scrittura su DB: il volume puo' essere alto (un report per
/// violazione, browser maliziosi possono floodare) e l'append-only log
/// strutturato e' sufficiente per analisi e ASR-tuning del CSP.
/// </remarks>
[ApiController]
[Route("api/security")]
[AllowAnonymous]
public class SecurityReportsController : ControllerBase
{
    // 8 KB sono ampiamente sufficienti per il singolo report CSP. Cap
    // hard per prevenire abuso. Caddy edge gia' limita a 25MB il body
    // request, qui restringiamo al livello applicativo per questo path.
    private const int MaxBodyBytes = 8 * 1024;

    private readonly ILogger<SecurityReportsController> _log;

    public SecurityReportsController(ILogger<SecurityReportsController> log)
    {
        _log = log;
    }

    [HttpPost("csp-report")]
    [EnableRateLimiting("csp-report")]
    [Consumes("application/csp-report", "application/reports+json", "application/json")]
    [RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> Report(CancellationToken ct)
    {
        // Leggi il body come byte[] per evitare di affidarci al model
        // binder (i due content-type CSP non sono nativamente bindabili
        // a DTO senza un input formatter dedicato).
        Request.EnableBuffering();
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms, ct);
        if (ms.Length == 0 || ms.Length > MaxBodyBytes)
        {
            // 204 sempre: non vogliamo dare informazioni utili a chi
            // tenta di sondare l'endpoint. Anche su body vuoto/troppo
            // grande, log a debug e via.
            _log.LogDebug("CSP report body vuoto o oversize ({Bytes} bytes)", ms.Length);
            return NoContent();
        }
        ms.Position = 0;

        try
        {
            using var doc = await JsonDocument.ParseAsync(ms, cancellationToken: ct);
            LogReport(doc.RootElement);
        }
        catch (JsonException ex)
        {
            _log.LogDebug(ex, "CSP report JSON non valido");
        }

        // Sempre 204: il browser non interpreta il body, lo standard chiede
        // un 2xx qualsiasi. 204 minimizza la risposta.
        return NoContent();
    }

    private void LogReport(JsonElement root)
    {
        // Formato legacy: { "csp-report": { ... } }
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("csp-report", out var legacy))
        {
            EmitCspEvent(legacy);
            return;
        }

        // Formato Reporting API: [ { "type": "csp-violation", "body": { ... } }, ... ]
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var report in root.EnumerateArray())
            {
                var type = TryStr(report, "type") ?? "unknown";
                if (string.Equals(type, "csp-violation", StringComparison.OrdinalIgnoreCase) &&
                    report.TryGetProperty("body", out var body))
                {
                    EmitCspEvent(body);
                }
                else
                {
                    _log.LogInformation(
                        "Browser report ricevuto (type={Type}, ua={UserAgent})",
                        type, Request.Headers.UserAgent.ToString());
                }
            }
            return;
        }

        // Edge case: payload sconosciuto. Logga la radice raw a debug.
        _log.LogDebug("CSP report con shape inattesa: {Json}", root.GetRawText());
    }

    private void EmitCspEvent(JsonElement body)
    {
        // Campi normalizzati tra il formato legacy (snake-case con trattini)
        // e quello Reporting API (camelCase / con trattino). Estraiamo
        // entrambi e prendiamo il primo non-null.
        var directive = TryStr(body, "violated-directive")
                     ?? TryStr(body, "effective-directive")
                     ?? TryStr(body, "effectiveDirective");
        var blocked   = TryStr(body, "blocked-uri") ?? TryStr(body, "blockedURL");
        var docUri    = TryStr(body, "document-uri") ?? TryStr(body, "documentURL");
        var source    = TryStr(body, "source-file") ?? TryStr(body, "sourceFile");
        var disposition = TryStr(body, "disposition"); // "enforce" | "report"
        var line   = TryInt(body, "line-number")   ?? TryInt(body, "lineNumber");
        var column = TryInt(body, "column-number") ?? TryInt(body, "columnNumber");

        _log.LogInformation(
            "CSP violation: directive={Directive} blocked={Blocked} document={Document} source={Source}:{Line}:{Column} disposition={Disposition} ip={Ip} ua={UserAgent}",
            directive, blocked, docUri, source, line, column, disposition,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());
    }

    private static string? TryStr(JsonElement el, string name)
        => el.ValueKind == JsonValueKind.Object &&
           el.TryGetProperty(name, out var v) &&
           v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static int? TryInt(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        if (!el.TryGetProperty(name, out var v)) return null;
        return v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;
    }
}
