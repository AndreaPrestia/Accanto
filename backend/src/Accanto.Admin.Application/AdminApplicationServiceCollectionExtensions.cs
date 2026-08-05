using Accanto.Admin.Application.Audit;
using Accanto.Admin.Application.Auth;
using Accanto.Admin.Application.Stats;
using Accanto.Admin.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Accanto.Admin.Application;

public static class AdminApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddAccantoAdminApplication(this IServiceCollection services)
    {
        services.AddScoped<IAdminAuthService, AdminAuthService>();
        services.AddScoped<IAdminPasswordResetService, AdminPasswordResetService>();
        services.AddScoped<IAdminUserOperationsService, AdminUserOperationsService>();
        services.AddScoped<IAdminAuditQueryService, AdminAuditQueryService>();
        services.AddScoped<IAdminStatsService, AdminStatsService>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
