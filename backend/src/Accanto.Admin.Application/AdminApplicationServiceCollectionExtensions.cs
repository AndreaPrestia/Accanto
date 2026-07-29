using Accanto.Admin.Application.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace Accanto.Admin.Application;

public static class AdminApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddAccantoAdminApplication(this IServiceCollection services)
    {
        services.AddScoped<IAdminAuthService, AdminAuthService>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
