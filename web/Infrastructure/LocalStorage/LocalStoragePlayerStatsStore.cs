using Microsoft.JSInterop;
using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Infrastructure.LocalStorage;

/// <summary>Stores only the anonymous browser identity; gameplay statistics live in PostgreSQL.</summary>
public sealed class LocalStoragePlayerStatsStore(ILocalStorageService localStorage) : IPlayerStatsStore
{
    private const string Key = "qsmpdle-player-stats";

    public async Task<PlayerStats> LoadAsync()
    {
        var identity = await localStorage.GetItemAsync<LocalPlayerIdentity>(Key);
        if (identity is null || identity.Id == Guid.Empty || identity.Version != PlayerStats.CurrentVersion)
        {
            identity ??= new LocalPlayerIdentity();
            if (identity.Id == Guid.Empty)
                identity.Id = Guid.NewGuid();
            identity.Version = PlayerStats.CurrentVersion;

            // Rewrite a missing or legacy PlayerStats object as identity-only data once, not on every read.
            await localStorage.SetItemAsync(Key, identity);
        }

        return new PlayerStats { Id = identity.Id, Version = identity.Version };
    }

    public async Task<Guid> GetPlayerIdAsync() => (await LoadAsync()).Id;

    public async Task SaveAsync(PlayerStats stats)
    {
        if (stats.Id == Guid.Empty)
            stats.Id = Guid.NewGuid();

        await localStorage.SetItemAsync(Key, new LocalPlayerIdentity
        {
            Id = stats.Id,
            Version = PlayerStats.CurrentVersion,
        });
    }

    public Task ClearAsync() => localStorage.ClearAsync().AsTask();

    private sealed class LocalPlayerIdentity
    {
        public int Version { get; set; } = PlayerStats.CurrentVersion;
        public Guid Id { get; set; } = Guid.NewGuid();
    }
}
