using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.Gameplay.Stores;

public interface IGameStateStore
{
    Task Init(string key);
    Task<GameState?> GetAsync();
    Task SaveAsync(GameState state);
    Task ClearAsync();
}
