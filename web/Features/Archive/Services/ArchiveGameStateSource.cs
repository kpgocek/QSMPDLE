using Microsoft.JSInterop;
using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.Archive.Services;

public sealed class ArchiveGameStateSource(ILocalStorageService localStorage, IJSRuntime js) : IArchiveGameStateSource
{
    private const string KeyPrefix = "qsmpdle-";

    private const string DayKeyPrefix = KeyPrefix + "canonical-";

    public async Task<GameState?> LoadAsync(int dayNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            // Canonical progress is shared by Daily and Archive.
            return await localStorage.GetItemAsync<GameState>($"{DayKeyPrefix}{dayNumber}");
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
            var keys = await js.InvokeAsync<string[]>("storageKeys", cancellationToken, DayKeyPrefix);
            var days = new HashSet<int>();
            foreach (var key in keys)
            {
                if (int.TryParse(key, out var day))
                {
                    days.Add(day);
                }
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
