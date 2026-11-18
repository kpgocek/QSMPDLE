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

    /// <summary>The game this browser made its first-ever guess in; <see cref="Guid.Empty"/> marks a player who predates the flag.</summary>
    Task<Guid?> GetFirstGameIdAsync() => Task.FromResult<Guid?>(null);
    Task SaveFirstGameIdAsync(Guid gameId) => Task.CompletedTask;

    /// <summary>True when a game other than the current one already holds a guess.</summary>
    Task<bool> HasGuessedInOtherGameAsync() => Task.FromResult(false);
}
