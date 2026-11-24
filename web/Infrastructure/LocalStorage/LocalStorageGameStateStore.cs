using Microsoft.JSInterop;
using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Infrastructure.LocalStorage;

public sealed class LocalStorageGameStateStore(ILocalStorageService localStorage, IJSRuntime js) : IGameStateStore
{
    private const string KeyPrefix = "qsmpdle-";
    private const int CurrentSchemaVersion = 3;
    private const string FirstGameKey = KeyPrefix + "first-game";

    private static readonly string[] GameKeyPrefixes =
        [KeyPrefix + "canonical-", KeyPrefix + "practice-", KeyPrefix + "daily-", KeyPrefix + "archive-"];

    private string Key { get; set; } = string.Empty;

    // Assign the key for the game state store, which will be used to store and retrieve the game state from local storage.
    public Task Init(string key)
    {
        Key = string.Concat(KeyPrefix, key);
        return Task.CompletedTask;
    }

    public async Task<GameState?> GetAsync()
    {
        if (!IsInitialized)
            throw new NullReferenceException(nameof(Key));
        try
        {
            var state = await localStorage.GetItemAsync<GameState>(Key);

            // If the persisted state has an older schema version, invalidate it so the UI
            // does not display stale or buggy data. Clients will re-create fresh state.
            if (state is not null && state.SchemaVersion != CurrentSchemaVersion)
            {
                await localStorage.RemoveItemAsync(Key);
                return null;
            }

            return state;
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveAsync(GameState state)
    {
        if (!IsInitialized)
            throw new NullReferenceException(nameof(Key));

        // Marked with the current schema version so future loads can detect compatibility.
        state.SchemaVersion = CurrentSchemaVersion;
        await localStorage.SetItemAsync(Key, state);
    }

    public async Task<Guid?> GetFirstGameIdAsync()
    {
        try
        {
            return await localStorage.GetItemAsync<Guid?>(FirstGameKey);
        }
        catch
        {
            return null;
        }
    }

    public Task SaveFirstGameIdAsync(Guid gameId) => localStorage.SetItemAsync(FirstGameKey, gameId).AsTask();

    public async Task<bool> HasGuessedInOtherGameAsync()
    {
        foreach (var prefix in GameKeyPrefixes)
        {
            foreach (var suffix in await GetKeySuffixesAsync(prefix))
            {
                var key = prefix + suffix;
                if (key == Key)
                    continue;

                try
                {
                    var state = await localStorage.GetItemAsync<GameState>(key);
                    if (state?.GuessesMade.Count > 0)
                        return true;
                }
                catch
                {
                    // An unreadable entry says nothing about whether the player has played.
                }
            }
        }

        return false;
    }

    public bool IsInitialized => !string.IsNullOrEmpty(Key);

    private Task<string[]> GetKeySuffixesAsync(string prefix) => js.InvokeAsync<string[]>("storageKeys", prefix).AsTask();
}
