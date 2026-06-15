using QSMPDLE.Web.Data.Gameplay;

namespace QSMPDLE.Web.Services;

public interface IGameStateStore
{
    Task Init(string key);
    Task<GameState?> GetAsync();
    Task SaveAsync(GameState state);
    Task ClearAsync();
}
