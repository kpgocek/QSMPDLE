using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Infrastructure.LocalStorage;

public interface IGameStateStore
{
    Task Init(string key);
    Task<GameState?> GetAsync();
    Task SaveAsync(GameState state);

    /// <summary>The game this browser made its first-ever guess in; <see cref="Guid.Empty"/> marks a player who predates the flag.</summary>
    Task<Guid?> GetFirstGameIdAsync();
    Task SaveFirstGameIdAsync(Guid gameId);

    /// <summary>True when a game other than the current one already holds a guess.</summary>
    Task<bool> HasGuessedInOtherGameAsync();
}
