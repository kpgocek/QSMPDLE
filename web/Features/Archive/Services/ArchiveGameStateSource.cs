using Microsoft.JSInterop;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Infrastructure.LocalStorage;

namespace QSMPDLE.Web.Features.Archive.Services;

public sealed class ArchiveGameStateSource(ILocalStorageService localStorage, IJSRuntime js) : IArchiveGameStateSource
{
    private const string KeyPrefix = "qsmpdle-";

    private static readonly string[] DayKeyPrefixes =
        [KeyPrefix + "canonical-", KeyPrefix + "daily-", KeyPrefix + "archive-"];

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

    public async Task<IReadOnlySet<int>?> GetStoredDayNumbersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var keys = await js.InvokeAsync<string[]>("storageKeys", cancellationToken);
            var days = new HashSet<int>();
            foreach (var key in keys)
            {
                var prefix = DayKeyPrefixes.FirstOrDefault(p => key.StartsWith(p, StringComparison.Ordinal));
                if (prefix is not null && int.TryParse(key.AsSpan(prefix.Length), out var day))
                    days.Add(day);
            }

            return days;
        }
        catch (JSException)
        {
            // Unknown, so the caller falls back to reading each day.
            return null;
        }
    }
}
