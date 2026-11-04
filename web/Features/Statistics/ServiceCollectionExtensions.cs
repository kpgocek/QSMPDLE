using Microsoft.Extensions.DependencyInjection.Extensions;
using QSMPDLE.Web.Features.Statistics.Services;
using QSMPDLE.Web.Infrastructure.LocalStorage;

namespace QSMPDLE.Web.Features.Statistics;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStatistics(this IServiceCollection services)
    {
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<IPlayerStatsStore, DatabasePlayerStatsStore>();
        services.AddScoped<IPlayerProfileService, PlayerProfileService>();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IGlobalStatsService, CachedGlobalStatsService>();

        return services;
    }
}
