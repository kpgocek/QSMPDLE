using System.Text.Json;
using Microsoft.JSInterop;
using QSMPDLE.Web.Features.Personalization.Models;

namespace QSMPDLE.Web.Infrastructure.LocalStorage;

public sealed class LocalStoragePreferencesStore(ILocalStorageService localStorage) : IPreferencesStore
{
    // Also read by the script at the top of App.razor.
    private const string Key = "qsmpdle-preferences";

    public async Task<PlayerPreferences> GetAsync()
    {
        try
        {
            return await localStorage.GetItemAsync<PlayerPreferences>(Key) ?? new PlayerPreferences();
        }
        catch (JsonException)
        {
            // A hand-edited or older value falls back to the defaults rather than breaking the page.
            return new PlayerPreferences();
        }
    }

    public Task SaveAsync(PlayerPreferences preferences) => localStorage.SetItemAsync(Key, preferences).AsTask();
}
