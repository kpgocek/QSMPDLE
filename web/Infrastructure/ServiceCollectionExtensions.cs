using QSMPDLE.Web.Infrastructure.LocalStorage;
using QSMPDLE.Web.Infrastructure.Persistence;
using QSMPDLE.Web.Features.Statistics.Services;
using QSMPDLE.Web.Services;

namespace QSMPDLE.Web.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ICharacterStore, CachedDatabaseCharacterStore>();
        services.AddScoped<IGameStateStore, LocalStorageGameStateStore>();
        services.AddScoped<ILegacyGameStateMigrationService, LegacyGameStateMigrationService>();
        services.AddScoped<LocalStoragePlayerStatsStore>();
        services.AddScoped<IPlayerStatsStore, DatabasePlayerStatsStore>();
        services.AddScoped<IGameStatsStore, DatabaseGameStatsStore>();
        services.AddScoped<IArchiveGameStateSource, ArchiveGameStateSource>();

        return services;
    }
}
