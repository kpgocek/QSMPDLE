using QSMPDLE.Web.Features.Communication.GameEvents;

namespace QSMPDLE.Web.Features.Communication;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInternalCommunication(this IServiceCollection services)
    {
        services.AddSingleton<IGameEventBus, GameEventBus>();

        return services;
    }
}
