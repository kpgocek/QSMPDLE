namespace QSMPDLE.Web.Themes;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddThemes(this IServiceCollection services)
    {
        services.AddScoped<ThemeService>();

        return services;
    }
}
