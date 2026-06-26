using QSMPDLE.Web.Infrastructure.LocalStorage;
using QSMPDLE.Web.Infrastructure.Persistence;

namespace QSMPDLE.Web.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ICharacterStore, CachedDatabaseCharacterStore>();
        services.AddScoped<IGameStateStore, LocalStorageGameStateStore>();
        services.AddScoped<IPlayerStatsStore, LocalStoragePlayerStatsStore>();
        services.AddScoped<IGameStatsStore, DatabaseGameStatsStore>();

        return services;
    }
}
