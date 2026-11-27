using QSMPDLE.Web.Features.Personalization.Models;

namespace QSMPDLE.Web.Infrastructure.LocalStorage;

public interface IPreferencesStore
{
    Task<PlayerPreferences> GetAsync();
    Task SaveAsync(PlayerPreferences preferences);
}
