using Accanto.Application.Auth;
using Accanto.Application.CareCircles;
using Accanto.Application.DoctorQuestions;
using Accanto.Application.Documents;
using Accanto.Application.Invites;
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

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICareCircleService, CareCircleService>();
        services.AddScoped<ITimelineService, TimelineService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IDoctorQuestionService, DoctorQuestionService>();
        services.AddScoped<ISharedUpdateService, SharedUpdateService>();
        services.AddScoped<IInviteService, InviteService>();

        services.AddSingleton<IDoctorQuestionTemplateProvider, StaticDoctorQuestionTemplateProvider>();
        services.AddSingleton<ISharedUpdateTemplateProvider, StaticSharedUpdateTemplateProvider>();

        return services;
    }
}
