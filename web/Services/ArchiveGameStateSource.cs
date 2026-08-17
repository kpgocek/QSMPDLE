using Microsoft.JSInterop;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Infrastructure.LocalStorage;

namespace QSMPDLE.Web.Services;

public sealed class ArchiveGameStateSource(ILocalStorageService localStorage) : IArchiveGameStateSource
{
    private const string KeyPrefix = "qsmpdle-";

    public async Task<GameState?> LoadAsync(GameMode mode, int dayNumber, CancellationToken cancellationToken = default)
    {
        var key = mode switch
        {
            GameMode.Daily => $"{KeyPrefix}daily-{dayNumber}",
            GameMode.Archive => $"{KeyPrefix}archive-{dayNumber}",
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

        try
        {
            return await localStorage.GetItemAsync<GameState>(key);
        }
        catch
        {
            return null;
        }
    }
}
