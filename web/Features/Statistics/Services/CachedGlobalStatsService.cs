using Microsoft.Extensions.Caching.Memory;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Infrastructure.Persistence;

namespace QSMPDLE.Web.Features.Statistics.Services;

/// <summary>
/// Serves the expensive site-wide aggregates from memory so a burst of Stats page views costs one
/// set of database scans per expiry window instead of one per visitor. Singleton by design: the
/// cached values contain no per-player data. Cached results are shared across circuits and must
/// be treated as read-only by callers.
/// </summary>
public sealed class CachedGlobalStatsService(IServiceScopeFactory scopeFactory, IMemoryCache cache, TimeProvider timeProvider) : IGlobalStatsService
{
    private static readonly TimeSpan Expiry = TimeSpan.FromMinutes(5);

    // One gate for all keys: concurrent misses wait for a single load instead of stampeding the
    // database, and loads for different keys are serialized, which also caps aggregate query load.
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    public Task<GlobalStatsView> GetGlobalStatsAsync() =>
        GetOrLoadAsync("global-stats", store => store.GetGlobalStatsAsync());

    public Task<List<DailyActivePlayersData>> GetDailyActivePlayersAsync(DateOnly? from) =>
        GetOrLoadAsync($"global-stats-dau-{from:O}", store => store.GetDailyActivePlayersAsync(from));

    public Task<List<NewVsReturningPlayersData>> GetNewVsReturningPlayersAsync(DateOnly? from) =>
        GetOrLoadAsync($"global-stats-new-vs-returning-{from:O}", store => store.GetNewVsReturningPlayersAsync(from));

    public Task<PlayerActivityDistributionData[]> GetPlayerActivityDistributionAsync() =>
        GetOrLoadAsync("global-stats-activity-distribution", store => store.GetPlayerActivityDistributionAsync());

    public Task<GamesPerPlayerStats> GetGamesPerPlayerStatsAsync() =>
        GetOrLoadAsync("global-stats-games-per-player", store => store.GetGamesPerPlayerStatsAsync());

    public Task<RetentionStats> GetRetentionStatsAsync() =>
        GetOrLoadAsync("global-stats-retention", store => store.GetRetentionStatsAsync());

    public Task<GlobalCharacterStats> GetGlobalCharacterStatsAsync() =>
        GetOrLoadAsync("global-stats-characters", store => store.GetGlobalCharacterStatsAsync());

    private async Task<T> GetOrLoadAsync<T>(string key, Func<IGameStatsStore, Task<T>> load)
    {
        if (cache.TryGetValue(key, out T? cached) && cached is not null)
            return cached;

        await _loadGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(key, out cached) && cached is not null)
                return cached;

            // The store is scoped; a short-lived scope keeps this singleton free of captive dependencies.
            await using var scope = scopeFactory.CreateAsyncScope();
            var value = await load(scope.ServiceProvider.GetRequiredService<IGameStatsStore>());

            cache.Set(key, value, timeProvider.GetUtcNow().Add(Expiry));
            return value;
        }
        finally
        {
            _loadGate.Release();
        }
    }
}
