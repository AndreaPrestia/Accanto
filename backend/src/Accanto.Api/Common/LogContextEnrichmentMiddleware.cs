using System.Security.Claims;
using Serilog.Context;

namespace Accanto.Api.Common;

/// <summary>
/// Pusha proprieta' diagnostiche (UserId, ClientIp, RequestId) nel
/// Serilog LogContext per la durata della request. Tutti i log emessi
/// successivamente nella pipeline (controller, servizi, EF) le ereditano
/// automaticamente -> filtering avanzato in Seq/Loki senza dover passare
/// l'ID ad ogni chiamata di log.
///
/// Va registrato DOPO UseAuthentication (per avere il ClaimsPrincipal) e
/// PRIMA degli endpoint. Il summary line di UseSerilogRequestLogging
/// vede comunque queste proprieta' perche' il context e' attivo per
/// tutta la durata della request.
/// </summary>
public sealed class LogContextEnrichmentMiddleware
{
    private readonly RequestDelegate _next;

    public LogContextEnrichmentMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? context.User?.FindFirstValue("sub");
        // X-Forwarded-For first hop quando dietro Caddy/reverse proxy. Se
        // assente o non considerato attendibile (proxy non configurato),
        // fallback al peer IP. Limitiamo a 45 char per evitare log
        // injection da header arbitrari.
        var ip = context.Connection.RemoteIpAddress?.ToString();
        var xff = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(xff))
        {
            var first = xff.Split(',')[0].Trim();
            if (first.Length <= 45) ip = first;
        }

        using (LogContext.PushProperty("RequestId", context.TraceIdentifier))
        using (LogContext.PushProperty("ClientIp", ip ?? "unknown"))
        using (LogContext.PushProperty("UserId", string.IsNullOrEmpty(userId) ? null : userId))
        {
            await _next(context);
        }
    }
}
