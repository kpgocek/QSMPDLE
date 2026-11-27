using QSMPDLE.Web.Features.Personalization.Services;

namespace QSMPDLE.Web.Features.Personalization;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPersonalization(this IServiceCollection services)
    {
        services.AddScoped<IPreferencesService, PreferencesService>();

        return services;
    }
}
