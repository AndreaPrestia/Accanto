using System.Threading.RateLimiting;
using Accanto.Admin.Api.Common;
using Accanto.Admin.Api.Middleware;
using Accanto.Admin.Application;
using Accanto.Admin.Application.Common.Security;
using Accanto.Admin.Infrastructure;
using Accanto.Admin.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// --- Logging (Serilog) -------------------------------------------------------
builder.Host.UseSerilog((ctx, services, logger) =>
{
    logger
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "accanto-admin-api")
        .Enrich.WithProperty("Environment", ctx.HostingEnvironment.EnvironmentName);

    if (ctx.HostingEnvironment.IsDevelopment())
        logger.WriteTo.Console();
    else
        logger.WriteTo.Console(new CompactJsonFormatter());

    var seqUrl = ctx.Configuration["Logging:SeqUrl"];
    if (!string.IsNullOrWhiteSpace(seqUrl))
    {
        var apiKey = ctx.Configuration["Logging:SeqApiKey"];
        logger.WriteTo.Seq(seqUrl, apiKey: string.IsNullOrWhiteSpace(apiKey) ? null : apiKey);
    }
});

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentAdmin, CurrentAdmin>();

builder.Services.AddAccantoAdminApplication();
builder.Services.AddAccantoAdminInfrastructure(builder.Configuration);

// --- CORS admin (separato dalla SPA pubblica) --------------------------------
var adminCors = (builder.Configuration["AdminCors:AllowedOrigins"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (adminCors.Length > 0)
        p.WithOrigins(adminCors).AllowAnyHeader().AllowAnyMethod();
    else
        p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
}));

// --- JWT admin (separato dal JWT pubblico) -----------------------------------
// Signing material risolto eager: fail-fast su config invalida e condivisione
// dello stesso snapshot tra AddJwtBearer e AdminJwtTokenService.
var adminJwt = builder.Configuration.GetSection("AdminJwt").Get<AdminJwtOptions>()!.ResolveSigningMaterial();
var adminIssuer = builder.Configuration["AdminJwt:Issuer"] ?? "accanto-admin";
var adminAudience = builder.Configuration["AdminJwt:Audience"] ?? "accanto-admin";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidIssuer = adminIssuer,
            ValidAudience = adminAudience,
            IssuerSigningKeyResolver = (_, _, kid, _) => adminJwt.Resolve(kid),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

// --- Rate limiting su login admin --------------------------------------------
builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opt.AddPolicy("admin-auth-login", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"admin-login:{ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Accanto Admin API", Version = "v1" });
});

var app = builder.Build();

// --- Migrazioni + seed (no-op in Testing) ------------------------------------
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AccantoAdminDbContext>();
    await db.Database.MigrateAsync();

    // Audit append-only a livello DB per il ruolo runtime admin.
    await db.Database.ExecuteSqlRawAsync(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'accanto_admin_app') THEN
        REVOKE UPDATE, DELETE ON TABLE public.admin_audit_logs FROM accanto_admin_app;
    END IF;
END$$;");

    // Seed SOLO in Development (mai in Production).
    if (app.Environment.IsDevelopment())
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AdminSeed");
        await AdminSeed.EnsureSeedAsync(app.Services, app.Configuration, logger);
    }
}

app.UseSerilogRequestLogging();
app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// --- Health ------------------------------------------------------------------
var startedAt = DateTimeOffset.UtcNow;
var appVersion = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "unknown";

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapGet("/health/ready", async (AccantoAdminDbContext db, CancellationToken ct) =>
{
    var uptime = (DateTimeOffset.UtcNow - startedAt).TotalSeconds;
    bool dbOk;
    string? dbError = null;
    try { dbOk = await db.Database.CanConnectAsync(ct); }
    catch (Exception ex) { dbOk = false; dbError = ex.GetType().Name; }

    var payload = new
    {
        status = dbOk ? "ok" : "degraded",
        version = appVersion,
        uptimeSeconds = (int)uptime,
        checks = new { db = dbOk ? "ok" : "down", dbError }
    };
    return dbOk ? Results.Ok(payload) : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

app.MapControllers();

app.Run();

public partial class Program { }
