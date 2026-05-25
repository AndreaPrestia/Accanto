using Accanto.Application.Account;
using Accanto.Application.Audit;
using Accanto.Application.Auth;
using Accanto.Application.Auth.TwoFactor;
using Accanto.Application.CareCircles;
using Accanto.Application.DoctorQuestions;
using Accanto.Application.Documents;
using Accanto.Application.Invites;
using Accanto.Application.Notifications;
using Accanto.Application.Security;
using Accanto.Application.SharedUpdates;
using Accanto.Application.Timeline;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Accanto.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddAccantoApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>(ServiceLifetime.Scoped);

        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<ITwoFactorService, TwoFactorService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IGdprExportService, GdprExportService>();
        services.AddScoped<ICareCircleService, CareCircleService>();
        services.AddScoped<ITimelineService, TimelineService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IDoctorQuestionService, DoctorQuestionService>();
        services.AddScoped<ISharedUpdateService, SharedUpdateService>();
        services.AddScoped<IInviteService, InviteService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ISecurityAuditQueryService, SecurityAuditQueryService>();
        services.AddScoped<INotificationPreferenceService, NotificationPreferenceService>();

        services.AddSingleton<IDoctorQuestionTemplateProvider, StaticDoctorQuestionTemplateProvider>();
        services.AddSingleton<ISharedUpdateTemplateProvider, StaticSharedUpdateTemplateProvider>();

        return services;
    }
}
