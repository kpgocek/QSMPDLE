using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Features.Gameplay.Stores;

namespace QSMPDLE.Web.Features.Gameplay;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGameplayFeature(this IServiceCollection services)
    {
        services.AddScoped<IGameService, GameService>();
        services.AddScoped<IGameStateStore, LocalStorageGameStateStore>();
        services.AddScoped<IPlayerStatsStore, LocalStoragePlayerStatsStore>();
        services.AddScoped<IGameStateManager, GameStateManager>();
        services.AddScoped<IGameplayOrchestrator, GameplayOrchestrator>();

        return services;
    }
}
