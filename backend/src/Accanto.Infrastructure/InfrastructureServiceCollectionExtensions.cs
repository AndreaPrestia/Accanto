using Accanto.Application.Audit;
using Accanto.Application.Common.Authorization;
using Accanto.Application.Email;
using Accanto.Application.Common.Persistence;
using Accanto.Application.Common.Security;
using Accanto.Application.Common.Storage;
using Accanto.Application.Documents;
using Accanto.Application.Export;
using Accanto.Application.Push;
using Accanto.Application.Security;
using Accanto.Application.Ai;
using Accanto.Infrastructure.Ai;
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
        services.Configure<ClamAvOptions>(configuration.GetSection("ClamAV"));

        // Malware scanner: ClamAV se ClamAV:Host e' configurato, altrimenti noop.
        // La factory rilegge il binding qui (non a runtime) perche' la
        // registrazione e' singleton: l'opt-in/opt-out richiede comunque
        // restart del backend.
        var clamHost = configuration.GetSection("ClamAV")["Host"];
        if (!string.IsNullOrWhiteSpace(clamHost))
        {
            services.AddSingleton<IMalwareScanner, ClamAvMalwareScanner>();
        }
        else
        {
            services.AddSingleton<IMalwareScanner, NoopMalwareScanner>();
        }

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

        // IAiAssistant: factory in base ad AiOptions.Provider.
        // "ollama" → OllamaAssistant con HttpClient dedicato (timeout settato runtime).
        // qualsiasi altro valore (incluso "none") → NullAiAssistant placeholder.
        // Nota: il gate 503 nel servizio AI usa AiOptions.IsConfigured prima di chiamare l'assistant.
        var aiSection = configuration.GetSection("Ai");
        var provider = aiSection["Provider"];
        if (string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IAiAssistant, OllamaAssistant>();
        }
        else
        {
            services.AddSingleton<IAiAssistant, NullAiAssistant>();
        }

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        return services;
    }
}
