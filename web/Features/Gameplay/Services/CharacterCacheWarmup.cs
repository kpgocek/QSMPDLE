using QSMPDLE.Web.Infrastructure.Persistence;

namespace QSMPDLE.Web.Features.Gameplay.Services;

/// <summary>
/// Loads the shared character catalog and today's target before the server takes traffic, so a
/// redeploy during a stream does not make the first wave of players wait on a cold cache.
/// </summary>
public sealed class CharacterCacheWarmup(
    IServiceScopeFactory scopeFactory,
    IDayService dayService,
    ILogger<CharacterCacheWarmup> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var characterStore = scope.ServiceProvider.GetRequiredService<ICharacterStore>();

            await characterStore.GetLookupsAsync(cancellationToken);
            await characterStore.GetCharacterForDayAsync(dayService.GetTodayDayNumber(), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Warm-up is an optimization; requests still load the cache on demand.
            logger.LogWarning(exception, "Character cache warm-up failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
