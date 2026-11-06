using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Infrastructure.Persistence;

public interface IGameStatsStore
{
    Task<GameSession> LoadOrNewAsync(Guid gameId);

    Task SaveAsync(GameSession stats);

    /// <summary>Records one guess without rewriting the rest of the session graph.</summary>
    async Task AddGuessAsync(Guid gameId, Guid playerId, int guessedCharacterId)
    {
        var stats = await LoadOrNewAsync(gameId);
        stats.PlayerId = playerId;
        stats.AddGuess(guessedCharacterId);
        await SaveAsync(stats);
    }

    /// <summary>Marks the session finished once; later calls leave the first result in place.</summary>
    async Task MarkFinishedAsync(Guid gameId, Guid playerId, DateTimeOffset finishedOnUtc, bool isWon)
    {
        var stats = await LoadOrNewAsync(gameId);
        if (stats.FinishedOnUtc.HasValue)
            return;

        stats.PlayerId = playerId;
        stats.FinishedOnUtc = finishedOnUtc;
        stats.IsWon = isWon;
        await SaveAsync(stats);
    }

    Task<GameSession?> GetActiveCanonicalSessionAsync(Guid playerId, int puzzleId) => Task.FromResult<GameSession?>(null);
    Task<GameSession> ClaimCanonicalSessionAsync(GameSession proposedSession) => Task.FromResult(proposedSession);

    Task<IEnumerable<GameSession>> GetPlayerGames(Guid playerId);

    Task<GlobalStatsView> GetGlobalStatsAsync();

    Task<List<DailyActivePlayersData>> GetDailyActivePlayersAsync(DateOnly? from);

    Task<List<NewVsReturningPlayersData>> GetNewVsReturningPlayersAsync(DateOnly? from);

    Task<PlayerActivityDistributionData[]> GetPlayerActivityDistributionAsync();

    Task<GamesPerPlayerStats> GetGamesPerPlayerStatsAsync();

    Task<RetentionStats> GetRetentionStatsAsync();

    Task<GlobalCharacterStats> GetGlobalCharacterStatsAsync();

    Task<PlayerCharacterStats> GetPlayerCharacterStatsAsync(Guid playerId);

    Task<DateOnly?> GetPlayerFirstGameDateAsync(Guid playerId);
    Task<List<int>> GetPlayerCompletedDailyNumbersAsync(Guid playerId);

    /// <summary>
    /// Returns all Daily-mode game sessions for the given player where the session's StartedOnUtc falls within the inclusive date range [start, end].
    /// Implementations MUST perform a single batched database query for the range.
    /// </summary>
    /// <summary>
    /// Returns all Daily-mode game sessions for the given player whose DailyNumber is between startNumber and endNumber (inclusive).
    /// Implementations MUST perform a single batched database query for the range.
    /// </summary>
    Task<List<GameSession>> GetPlayerDailyGamesByNumberRangeAsync(Guid playerId, int startNumber, int endNumber);

    Task<List<GameSession>> GetPlayerCanonicalGamesByPuzzleRangeAsync(Guid playerId, int startPuzzleId, int endPuzzleId) => Task.FromResult(new List<GameSession>());
}
