using QSMPDLE.Web.Features.Personalization.Models;

namespace QSMPDLE.Web.Features.Personalization.Services;

public interface IPreferencesService
{
    Task<PlayerPreferences> GetAsync();

    /// <summary>Saves the preferences, with the name cleaned, and applies them to the open page at once.</summary>
    Task<PlayerPreferences> SaveAsync(PlayerPreferences preferences);

    /// <summary>The player's name for share text, or null when none is set or it can't be read.</summary>
    Task<string?> GetDisplayNameAsync();
}
