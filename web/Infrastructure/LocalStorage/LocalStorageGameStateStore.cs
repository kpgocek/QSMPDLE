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

    public async Task<GameState?> GetByKeyAsync(string key)
    {
        try
        {
            // Legacy states use schema v2. Read them without invalidating so the
            // state manager can move compatible progress to the canonical key.
            return await localStorage.GetItemAsync<GameState>(string.Concat(KeyPrefix, key));
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<int>> GetLegacyPuzzleIdsAsync()
    {
        var puzzleIds = new HashSet<int>();

        foreach (var key in await GetKeysAsync())
        {
            var suffix = key.StartsWith($"{KeyPrefix}daily-", StringComparison.Ordinal)
                ? key[$"{KeyPrefix}daily-".Length..]
                : key.StartsWith($"{KeyPrefix}archive-", StringComparison.Ordinal)
                    ? key[$"{KeyPrefix}archive-".Length..]
                    : null;

            if (suffix is not null && int.TryParse(suffix, out var puzzleId) && puzzleId > 0)
                puzzleIds.Add(puzzleId);
        }

        return puzzleIds.ToList();
    }

    public Task SaveByKeyAsync(string key, GameState state)
    {
        state.SchemaVersion = CurrentSchemaVersion;
        return localStorage.SetItemAsync(string.Concat(KeyPrefix, key), state).AsTask();
    }

    public Task RemoveByKeyAsync(string key) => localStorage.RemoveItemAsync(string.Concat(KeyPrefix, key)).AsTask();

    public async Task SaveAsync(GameState state)
    {
        if (!IsInitialized)
            throw new NullReferenceException(nameof(Key));

        // Ensure stored state is marked with the current schema version so future loads
        // can detect compatibility.
        await SaveByKeyAsync(Key[KeyPrefix.Length..], state);
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
        foreach (var key in await GetKeysAsync())
        {
            if (key == Key || !GameKeyPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal)))
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

        return false;
    }

    public async Task ClearAsync()
    {
        if (!IsInitialized)
            throw new NullReferenceException(nameof(Key));

        await localStorage.RemoveItemAsync(Key);
    }

    public bool IsInitialized => !string.IsNullOrEmpty(Key);

    private Task<string[]> GetKeysAsync() => js.InvokeAsync<string[]>("storageKeys").AsTask();
}
