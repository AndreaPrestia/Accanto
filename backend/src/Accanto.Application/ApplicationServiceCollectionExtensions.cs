using Accanto.Application.Account;
using Accanto.Application.Ai;
using Accanto.Application.Ai.Guardrails;
using Accanto.Application.Audit;
using Accanto.Application.Auth;
using Accanto.Application.Auth.TwoFactor;
using Accanto.Application.CareCircles;
using Accanto.Application.DoctorQuestions;
using Accanto.Application.Documents;
using Accanto.Application.Internal;
using Accanto.Application.Invites;
using Accanto.Application.Notifications;
using Accanto.Application.Push;
using Accanto.Application.Security;
using Accanto.Application.SharedUpdates;
using Accanto.Application.Timeline;
using Accanto.Application.Wellbeing;
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
        services.AddScoped<IOwnerTwoFactorOnboarding, OwnerTwoFactorOnboarding>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IUserErasureService, UserErasureService>();
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
        services.AddScoped<IDevicePushTokenService, DevicePushTokenService>();
        services.AddScoped<ICheckInService, CheckInService>();

        // Endpoint interni service-to-service per il control plane admin.
        services.AddScoped<IInternalUserMetadataService, InternalUserMetadataService>();
        services.AddScoped<IInternalAdminAccountService, InternalAdminAccountService>();

        services.AddSingleton<IDoctorQuestionTemplateProvider, StaticDoctorQuestionTemplateProvider>();
        services.AddSingleton<ISharedUpdateTemplateProvider, StaticSharedUpdateTemplateProvider>();

        // AI: il PromptBuilder è stateless. IAiAssistant viene registrato in Infrastructure
        // (factory in base ad AiOptions.Provider: "none" → NullAiAssistant, "ollama" → OllamaAssistant).
        services.AddSingleton<AiPromptBuilder>();
        services.AddSingleton<InputGuardrail>();
        services.AddSingleton<OutputGuardrail>();
        services.AddSingleton<AiIdempotencyCache>();
        services.AddScoped<IAiInteractionStore, AiInteractionStore>();
        services.AddScoped<IAiService, AiService>();

        return services;
    }
}
