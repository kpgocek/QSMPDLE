using Microsoft.JSInterop;
using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Infrastructure.LocalStorage;

public sealed class LocalStorageGameStateStore(ILocalStorageService localStorage) : IGameStateStore
{
    private const string KeyPrefix = "qsmpdle-";
    private const int CurrentSchemaVersion = 2;

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

        // Ensure stored state is marked with the current schema version so future loads
        // can detect compatibility.
        state.SchemaVersion = CurrentSchemaVersion;

        await localStorage.SetItemAsync(Key, state);
    }

    public async Task ClearAsync()
    {
        if (!IsInitialized)
            throw new NullReferenceException(nameof(Key));

        await localStorage.RemoveItemAsync(Key);
    }

    public bool IsInitialized => !string.IsNullOrEmpty(Key);
}
