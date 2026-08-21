using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Infrastructure.LocalStorage;

public interface IGameStateStore
{
    Task Init(string key);
    Task<GameState?> GetAsync();
    Task<GameState?> GetByKeyAsync(string key) => Task.FromResult<GameState?>(null);
    Task<IReadOnlyList<int>> GetLegacyPuzzleIdsAsync() => Task.FromResult<IReadOnlyList<int>>([]);
    Task SaveByKeyAsync(string key, GameState state) => Task.CompletedTask;
    Task RemoveByKeyAsync(string key) => Task.CompletedTask;
    Task SaveAsync(GameState state);
    Task ClearAsync();
}
