using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Infrastructure.Persistence;

public interface IGameStatsStore
{
    Task<GameSession> LoadOrNewAsync(Guid gameId);

    Task SaveAsync(GameSession stats);

    Task<IEnumerable<GameSession>> GetPlayerGames(Guid playerId);

    Task<GameSession?> GetPlayerCompletedDailyGameAsync(Guid playerId, int dailyNumber);

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
}
