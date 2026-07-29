using Accanto.Admin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        return services;
    }
}
