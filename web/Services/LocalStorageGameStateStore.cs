using Microsoft.JSInterop;
using QSMPDLE.Web.Data.Gameplay;

namespace QSMPDLE.Web.Services;

public sealed class LocalStorageGameStateStore(ILocalStorageService localStorage) : IGameStateStore
{
    private string Key { get; set; } = null!;

    public async Task Init(string key)
    {
        Key = key;
    }

    public async Task<GameState?> GetAsync()
    {
        if (Key is null) throw new NullReferenceException(nameof(Key));
        return await localStorage.GetItemAsync<GameState>(Key);
    }

    public async Task SaveAsync(GameState state)
    {
        if (Key is null) throw new NullReferenceException(nameof(Key));
        await localStorage.SetItemAsync(Key, state);
    }

    public async Task ClearAsync()
    {
        if (Key is null) throw new NullReferenceException(nameof(Key));
        await localStorage.RemoveItemAsync(Key);
    }
}
