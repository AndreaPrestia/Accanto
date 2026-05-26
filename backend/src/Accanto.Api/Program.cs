using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Accanto.Api.Common;
using Accanto.Api.Configuration;
using Accanto.Application;
using Accanto.Application.Auth;
using Accanto.Infrastructure;
using Accanto.Infrastructure.Persistence;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddAccantoApplication();
builder.Services.AddAccantoInfrastructure(builder.Configuration);

// CORS
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (allowedOrigins.Length > 0)
        p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    else
        p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
}));

// JWT
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key mancante in configurazione.");
var jwtIssuer = jwtSection["Issuer"] ?? "accanto";
var jwtAudience = jwtSection["Audience"] ?? "accanto";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

// Refresh token: scadenza (giorni) dalla stessa sezione Jwt usata per l'access token.
builder.Services.Configure<RefreshTokenOptions>(o =>
{
    var days = builder.Configuration.GetValue<int?>("Jwt:RefreshTokenExpiryDays");
    if (days is > 0) o.ExpiryDays = days.Value;
});

// Lockout dopo N tentativi di login falliti. Configurabile via env Lockout:*.
builder.Services.Configure<LockoutOptions>(builder.Configuration.GetSection("Lockout"));

// 2FA TOTP: issuer per QR code + durata challenge.
builder.Services.Configure<Accanto.Application.Auth.TwoFactor.TwoFactorOptions>(builder.Configuration.GetSection("TwoFactor"));

// Rate limiting su endpoint sensibili (login/register/cambio password/invio inviti)
var rateLimits = builder.Configuration.GetSection("RateLimit").Get<RateLimitOptions>() ?? new RateLimitOptions();
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection("RateLimit"));
builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opt.AddPolicy("auth-login", ctx => BuildPartition(IpKey(ctx, "login"), rateLimits.Login));
    opt.AddPolicy("auth-register", ctx => BuildPartition(IpKey(ctx, "register"), rateLimits.Register));
    opt.AddPolicy("auth-sensitive", ctx => BuildPartition(UserOrIpKey(ctx, "sensitive"), rateLimits.Sensitive));
    opt.AddPolicy("invite-create", ctx => BuildPartition(UserOrIpKey(ctx, "invite"), rateLimits.InviteCreate));
    opt.AddPolicy("ai", ctx => BuildPartition(UserOrIpKey(ctx, "ai"), rateLimits.Ai));
});

static string IpKey(HttpContext ctx, string scope)
    => $"{scope}:{ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

static string UserOrIpKey(HttpContext ctx, string scope)
{
    var sub = ctx.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
              ?? ctx.User?.FindFirst("sub")?.Value;
    return sub is { Length: > 0 }
        ? $"{scope}:user:{sub}"
        : $"{scope}:ip:{ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
}

static RateLimitPartition<string> BuildPartition(string key, RateLimitPolicyOptions p)
    => RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = p.PermitLimit,
        Window = p.Window,
        QueueLimit = 0,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        AutoReplenishment = true,
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Accanto API", Version = "v1" });
    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Inserisci 'Bearer {token}'",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    };
    c.AddSecurityDefinition("Bearer", jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Id = "Bearer", Type = ReferenceType.SecurityScheme } }] = new List<string>()
    });
});

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AccantoDbContext>();
    await db.Database.MigrateAsync();
}

app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapControllers();

app.Run();

public partial class Program { }
