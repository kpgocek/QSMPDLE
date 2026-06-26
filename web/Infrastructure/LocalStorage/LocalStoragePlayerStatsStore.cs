using Microsoft.JSInterop;
using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Infrastructure.LocalStorage;

public sealed class LocalStoragePlayerStatsStore(ILocalStorageService LocalStorage) : IPlayerStatsStore
{
    private const string Key = "qsmpdle-player-stats";

    public async Task<PlayerStats> LoadAsync()
    {
        var stats = await LocalStorage.GetItemAsync<PlayerStats>(Key)
           ?? new PlayerStats();

        if (stats.Id == Guid.Empty)
        {
            stats.Id = Guid.NewGuid();

            await SaveAsync(stats);
        }

        return stats;
    }

    public async Task SaveAsync(PlayerStats stats)
    {
        await LocalStorage.SetItemAsync(Key, stats);
    }

    public async Task ClearAsync()
    {
        await LocalStorage.ClearAsync();
    }
}
