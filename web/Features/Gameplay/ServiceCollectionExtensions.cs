using QSMPDLE.Web.Features.Gameplay.Services;

namespace QSMPDLE.Web.Features.Gameplay;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGameplay(this IServiceCollection services)
    {
        services.AddScoped<IGameService, GameService>();
        services.AddScoped<IGameStateManager, GameStateManager>();

        return services;
    }
}
