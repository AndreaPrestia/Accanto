using Accanto.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Accanto.Api.Common;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppValidationException ex)
        {
            await Write(context, StatusCodes.Status422UnprocessableEntity, "Dati non validi.", ex.Errors);
        }
        catch (NotFoundException ex)
        {
            await Write(context, StatusCodes.Status404NotFound, ex.Message);
        }
        catch (ForbiddenException ex)
        {
            await Write(context, StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (ConflictException ex)
        {
            await Write(context, StatusCodes.Status409Conflict, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            await Write(context, StatusCodes.Status401Unauthorized, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore non gestito");
            var detail = (_env.IsDevelopment() || _env.EnvironmentName == "Testing") ? ex.ToString() : null;
            await Write(context, StatusCodes.Status500InternalServerError, "Errore interno del server.", null, detail);
        }
    }

    private static async Task Write(HttpContext ctx, int status, string title, IReadOnlyDictionary<string, string[]>? errors = null, string? detail = null)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";

        if (errors is not null)
        {
            var vp = new ValidationProblemDetails(errors.ToDictionary(kv => kv.Key, kv => kv.Value))
            {
                Status = status,
                Title = title
            };
            await ctx.Response.WriteAsJsonAsync(vp);
        }
        else
        {
            var pd = new ProblemDetails { Status = status, Title = title, Detail = detail };
            await ctx.Response.WriteAsJsonAsync(pd);
        }
    }
}
