using Microsoft.Extensions.DependencyInjection.Extensions;
using QSMPDLE.Web.Diagnostics;
using QSMPDLE.Web.Features.Communication.GameEvents;

namespace QSMPDLE.Web.Features.Communication;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInternalCommunication(this IServiceCollection services)
    {
        services.TryAddSingleton<RuntimeCounters>();
        services.AddScoped<IGameEventBus, GameEventBus>();

        return services;
    }
}
