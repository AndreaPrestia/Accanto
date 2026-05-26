using Accanto.Application.Audit;
using Accanto.Application.Common.Authorization;
using Accanto.Application.Email;
using Accanto.Application.Common.Persistence;
using Accanto.Application.Common.Security;
using Accanto.Application.Common.Storage;
using Accanto.Application.Export;
using Accanto.Application.Push;
using Accanto.Application.Security;
using Accanto.Application.Ai;
using Accanto.Infrastructure.Audit;
using Accanto.Infrastructure.Authorization;
using Accanto.Infrastructure.Email;
using Accanto.Infrastructure.Export;
using Accanto.Infrastructure.Persistence;
using Accanto.Infrastructure.Push;
using Accanto.Infrastructure.Security;
using Accanto.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Accanto.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddAccantoInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AccantoDbContext>(opt =>
        {
            var conn = configuration.GetConnectionString("Postgres");
            opt.UseNpgsql(conn);
        });
        services.AddScoped<IAccantoDbContext>(sp => sp.GetRequiredService<AccantoDbContext>());

        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.Configure<StorageOptions>(configuration.GetSection("Storage"));
        services.Configure<EncryptionOptions>(configuration.GetSection("Encryption"));
        services.Configure<PushOptions>(configuration.GetSection("Push"));
        services.Configure<EmailOptions>(configuration.GetSection("Email"));
        services.Configure<AiOptions>(configuration.GetSection("Ai"));

        services.AddSingleton<IFieldProtector, AesGcmFieldProtector>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddScoped<ICareCircleAuthorization, CareCircleAuthorization>();
        services.AddSingleton<IPushService, PushService>();
        services.AddSingleton<IAuditLog, AuditLog>();
        services.AddSingleton<ISecurityAuditLog, SecurityAuditLog>();
        services.AddSingleton<IEmailService, EmailService>();
        services.AddSingleton<ICircleEmailNotifier, CircleEmailNotifier>();
        services.AddScoped<ICareCircleExportService, CareCircleExportService>();

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        return services;
    }
}
