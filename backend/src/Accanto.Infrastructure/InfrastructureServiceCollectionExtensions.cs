using Accanto.Application.Common.Authorization;
using Accanto.Application.Common.Persistence;
using Accanto.Application.Common.Security;
using Accanto.Application.Common.Storage;
using Accanto.Infrastructure.Authorization;
using Accanto.Infrastructure.Persistence;
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

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddScoped<ICareCircleAuthorization, CareCircleAuthorization>();

        return services;
    }
}
