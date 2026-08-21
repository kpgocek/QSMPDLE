using Microsoft.JSInterop;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Infrastructure.LocalStorage;

namespace QSMPDLE.Web.Services;

public sealed class ArchiveGameStateSource(ILocalStorageService localStorage) : IArchiveGameStateSource
{
    private const string KeyPrefix = "qsmpdle-";

    public async Task<GameState?> LoadAsync(GameMode mode, int dayNumber, CancellationToken cancellationToken = default)
    {
        var legacyKey = mode switch
        {
            GameMode.Daily => $"{KeyPrefix}daily-{dayNumber}",
            GameMode.Archive => $"{KeyPrefix}archive-{dayNumber}",
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

        try
        {
            // Canonical progress is shared by Daily and Archive. Fall back to
            // the legacy per-mode key for a browser that has not been opened
            // since the storage migration.
            var canonical = await localStorage.GetItemAsync<GameState>($"{KeyPrefix}canonical-{dayNumber}");
            return canonical ?? await localStorage.GetItemAsync<GameState>(legacyKey);
        }
        catch
        {
            return null;
        }
    }
}
