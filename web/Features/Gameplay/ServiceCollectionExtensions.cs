using Microsoft.Extensions.DependencyInjection.Extensions;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;

namespace QSMPDLE.Web.Features.Gameplay;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGameplay(this IServiceCollection services)
    {
        services.AddOptions<GameplayOptions>().BindConfiguration(GameplayOptions.SectionName);
        services.AddSingleton<IDayService, DayService>();
        services.AddSingleton<IGameplayAvailabilityService, GameplayAvailabilityService>();
        services.AddScoped<IGameService, GameService>();
        services.AddScoped<ICharacterComparer, CharacterComparer>();
        services.AddScoped<IGameStateManager, GameStateManager>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IDailyScheduleService, DailyScheduleService>();

        services.AddHostedService<CharacterCacheWarmup>();
        services.AddHostedService<DailyScheduleWorker>();

        return services;
    }
}
