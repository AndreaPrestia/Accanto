using System.Text.Json;
using Accanto.Admin.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Accanto.Admin.Api.Middleware;

/// <summary>
/// Traduce le eccezioni applicative in ProblemDetails JSON coerenti.
/// Non espone mai stacktrace ne' dettagli interni in produzione.
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        var (status, title) = ex switch
        {
            AdminValidationException => (StatusCodes.Status422UnprocessableEntity, "ValidationError"),
            AdminUnauthorizedException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            AdminForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
            AdminNotFoundException => (StatusCodes.Status404NotFound, "NotFound"),
            _ => (StatusCodes.Status500InternalServerError, "InternalError")
        };

        if (status >= 500)
            _logger.LogError(ex, "Errore non gestito nella Admin API");

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status >= 500 ? "Errore interno." : ex.Message
        };

        if (ex is AdminValidationException vex && vex.Errors.Count > 0)
            problem.Extensions["errors"] = vex.Errors;

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}
