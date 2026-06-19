using Microsoft.JSInterop;
using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.Gameplay.Stores;

public sealed class LocalStoragePlayerStatsStore : IPlayerStatsStore
{
    private readonly ILocalStorageService _localStorage;

    private const string Key = "qsmpdle-player-stats";

    public LocalStoragePlayerStatsStore(
        ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task<PlayerStats> LoadAsync()
    {
        return await _localStorage.GetItemAsync<PlayerStats>(Key)
            ?? new PlayerStats();
    }

    public async Task SaveAsync(PlayerStats stats)
    {
        await _localStorage.SetItemAsync(Key, stats);
    }

    public async Task ClearAsync()
    {
        await _localStorage.ClearAsync();
    }
}
