using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Infrastructure.Persistence;

namespace QSMPDLE.Web.Features.Statistics.Services;

/// <summary>
///     Serves the site-wide totals from memory, since every page's footer shows them: one load per
///     expiry window instead of one per page view. Singleton by design: the totals hold no per-player data.
/// </summary>
public sealed class CachedGlobalStatsService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IMemoryCache cache,
    TimeProvider timeProvider) : IGlobalStatsService
{
    private const string TotalsKey = "global-stats-totals";
    private static readonly TimeSpan Expiry = TimeSpan.FromMinutes(5);

    // Concurrent misses wait for a single load instead of stampeding the database.
    private readonly SemaphoreSlim loadGate = new(1, 1);

    public async Task<GlobalTotals> GetTotalsAsync()
    {
        if (cache.TryGetValue(TotalsKey, out GlobalTotals? cached) && cached is not null)
        {
            return cached;
        }

        await loadGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(TotalsKey, out cached) && cached is not null)
            {
                return cached;
            }

            await using var database = await dbContextFactory.CreateDbContextAsync();
            // Abandoned games are left out: a game, and its player, count once it is won or lost.
            var totals = await database.GameStats.AsNoTracking()
                .Where(game => !game.IsLegacyDuplicate)
                .Where(GameSession.IsDecided)
                // One group, so both numbers come from one pass over the games.
                .GroupBy(_ => 1)
                .Select(games =>
                    new GlobalTotals(games.LongCount(), games.Select(game => game.PlayerId).Distinct().LongCount()))
                .SingleOrDefaultAsync() ?? new GlobalTotals(0, 0);

            cache.Set(TotalsKey, totals, timeProvider.GetUtcNow().Add(Expiry));
            return totals;
        }
        finally
        {
            loadGate.Release();
        }
    }
}
