using Microsoft.JSInterop;
using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Infrastructure.LocalStorage;

public sealed class LocalStoragePlayerStatsStore(ILocalStorageService LocalStorage) : IPlayerStatsStore
{
    private const string Key = "qsmpdle-player-stats";
    private const int GuessDistributionSize = 6;

    public async Task<PlayerStats> LoadAsync()
    {
        var stats = await LocalStorage.GetItemAsync<PlayerStats>(Key);
        var shouldSave = false;

        if (stats is null)
        {
            stats = new PlayerStats();
            shouldSave = true;
        }

        if (stats.Id == Guid.Empty)
        {
            stats.Id = Guid.NewGuid();
            shouldSave = true;
        }

        if (stats.GuessDistribution is not { Length: GuessDistributionSize })
        {
            stats.GuessDistribution = ResizeGuessDistribution(stats.GuessDistribution);
            shouldSave = true;
        }

        if (stats.Version < PlayerStats.CurrentVersion)
        {
            stats.Version = PlayerStats.CurrentVersion;
            shouldSave = true;
        }

        if (shouldSave)
        {
            await SaveAsync(stats);
        }

        return stats;
    }

    public async Task SaveAsync(PlayerStats stats)
    {
        if (stats.Id == Guid.Empty)
        {
            stats.Id = Guid.NewGuid();
        }

        if (stats.GuessDistribution is not { Length: GuessDistributionSize })
        {
            stats.GuessDistribution = ResizeGuessDistribution(stats.GuessDistribution);
        }

        stats.Version = PlayerStats.CurrentVersion;

        await LocalStorage.SetItemAsync(Key, stats);
    }

    public async Task ClearAsync()
    {
        await LocalStorage.ClearAsync();
    }

    private static int[] ResizeGuessDistribution(int[]? guessDistribution)
    {
        var resized = new int[GuessDistributionSize];
        if (guessDistribution is not null)
        {
            Array.Copy(guessDistribution, resized, Math.Min(guessDistribution.Length, GuessDistributionSize));
        }

        return resized;
    }
}
