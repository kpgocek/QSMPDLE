using Microsoft.JSInterop;
using QSMPDLE.Web.Features.Personalization.Models;
using QSMPDLE.Web.Infrastructure.LocalStorage;

namespace QSMPDLE.Web.Features.Personalization.Services;

public sealed class PreferencesService(IPreferencesStore preferencesStore, IJSRuntime js) : IPreferencesService
{
    public Task<PlayerPreferences> GetAsync() => preferencesStore.GetAsync();

    public async Task<PlayerPreferences> SaveAsync(PlayerPreferences preferences)
    {
        preferences = preferences with { DisplayName = PlayerPreferences.CleanName(preferences.DisplayName) };
        await preferencesStore.SaveAsync(preferences);
        // Defined by the script at the top of App.razor, which applies the same values on every page load.
        await js.InvokeVoidAsync("applyPreferences", preferences);
        return preferences;
    }

    public async Task<string?> GetDisplayNameAsync()
    {
        try
        {
            return PlayerPreferences.CleanName((await preferencesStore.GetAsync()).DisplayName);
        }
        catch (JSException)
        {
            // Sharing still works, with "I".
            return null;
        }
    }
}
