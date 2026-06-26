using QSMPDLE.Web.Features.Statistics.Services;

namespace QSMPDLE.Web.Features.Statistics;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStatistics(this IServiceCollection services)
    {
        services.AddScoped<IStatisticsService, StatisticsService>();

        return services;
    }
}
