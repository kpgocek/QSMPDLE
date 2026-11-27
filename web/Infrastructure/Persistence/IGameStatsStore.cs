using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Infrastructure.Persistence;

public interface IGameStatsStore
{
    /// <summary>
    ///     Records the player's whole board, including its newest guess, and the outcome in one step. Returns the
    ///     stored game, which differs from <paramref name="game" /> or its guesses when another tab got there first.
    /// </summary>
    Task<GameSession> RecordGuessAsync(GameSession game, IReadOnlyList<int> guessedCharacterIds,
        DateTimeOffset recordedOnUtc);

    Task<GameSession?> GetActiveCanonicalSessionAsync(Guid playerId, int puzzleId) =>
        Task.FromResult<GameSession?>(null);

    Task<IEnumerable<GameSession>> GetPlayerGames(Guid playerId, SessionCategory? category = null);

    /// <summary>One batched query for the archive calendar: the player's canonical sessions for a puzzle range.</summary>
    Task<List<GameSession>>
        GetPlayerCanonicalGamesByPuzzleRangeAsync(Guid playerId, int startPuzzleId, int endPuzzleId);
}
