using Accanto.Admin.Application.Audit;
using Accanto.Admin.Application.Common.Persistence;
using Accanto.Admin.Application.Common.Security;
using Accanto.Admin.Application.Email;
using Accanto.Admin.Application.Users;
using Accanto.Admin.Infrastructure.Audit;
using Accanto.Admin.Infrastructure.Email;
using Accanto.Admin.Infrastructure.Internal;
using Accanto.Admin.Infrastructure.Persistence;
using Accanto.Admin.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Accanto.Admin.Infrastructure;

public static class AdminInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registra l'accesso al database admin (AccantoAdminDb) usando la connection
    /// string dedicata <c>ConnectionStrings:AdminDatabase</c>. Separata da quella
    /// pubblica (<c>ConnectionStrings:Postgres</c>): i due DB non condividono nulla.
    /// </summary>
    public static IServiceCollection AddAccantoAdminInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AccantoAdminDbContext>(opt =>
        {
            var conn = configuration.GetConnectionString("AdminDatabase");
            opt.UseNpgsql(conn);
        });
        services.AddScoped<IAccantoAdminDbContext>(sp => sp.GetRequiredService<AccantoAdminDbContext>());

        // JWT admin: sezione dedicata AdminJwt (issuer/audience/chiavi separati da Jwt pubblico).
        services.Configure<AdminJwtOptions>(configuration.GetSection("AdminJwt"));
        services.AddSingleton<AdminJwtSigningMaterial>(sp =>
            sp.GetRequiredService<IOptions<AdminJwtOptions>>().Value.ResolveSigningMaterial());

        services.AddScoped<IAdminJwtTokenService, AdminJwtTokenService>();
        services.AddSingleton<IAdminPasswordHasher, AdminPasswordHasher>();
        services.AddScoped<IAdminAuditLog, AdminAuditLogWriter>();

        // Email admin (SMTP MailKit). Sezione AdminEmail; no-op se non configurato.
        services.Configure<AdminEmailOptions>(configuration.GetSection("AdminEmail"));
        services.AddScoped<IAdminEmailSender, AdminEmailSender>();

        // Client service-to-service verso gli endpoint interni della app pubblica.
        services.Configure<InternalAppOptions>(configuration.GetSection("InternalApp"));
        services.AddSingleton<InternalServiceTokenIssuer>();
        services.AddHttpClient<IInternalAppClient, InternalAppClient>((sp, client) =>
        {
            var opt = sp.GetRequiredService<IOptions<InternalAppOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(opt.BaseUrl))
                client.BaseAddress = new Uri(opt.BaseUrl, UriKind.Absolute);
        });

        return services;
    }
}
