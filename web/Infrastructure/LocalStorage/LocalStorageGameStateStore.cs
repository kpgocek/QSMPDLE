using Microsoft.JSInterop;
using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Infrastructure.LocalStorage;

public sealed class LocalStorageGameStateStore(ILocalStorageService localStorage) : IGameStateStore
{
    private const string KeyPrefix = "qsmpdle-";

    private string Key { get; set; } = string.Empty;

    // Assign the key for the game state store, which will be used to store and retrieve the game state from local storage.
    public async Task Init(string key)
    {
        Key = string.Concat(KeyPrefix, key);
    }

    public async Task<GameState?> GetAsync()
    {
        if (!IsInitialized)
            throw new NullReferenceException(nameof(Key));

        return await localStorage.GetItemAsync<GameState>(Key);
    }

    public async Task SaveAsync(GameState state)
    {
        if (!IsInitialized)
            throw new NullReferenceException(nameof(Key));

        Console.WriteLine(Key);

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
