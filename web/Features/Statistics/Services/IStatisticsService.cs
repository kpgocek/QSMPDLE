using QSMPDLE.Web.Features.Communication.GameEvents;
using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Features.Statistics.Services;

public interface IStatisticsService
{
    // EVENTS
    Task RecordGameStartedAsync(GameStartedEvent eventData);
    Task RecordGuessMadeAsync(GuessMadeEvent eventData);
    Task RecordGameFinishedAsync(GameFinishedEvent eventData);
    Task<GameSession?> GetActiveCanonicalSessionAsync(Guid playerId, int puzzleId) => Task.FromResult<GameSession?>(null);
    Task<GameSession> ClaimCanonicalSessionAsync(GameSession proposedSession) => Task.FromResult(proposedSession);


    // ENDPOINTS
    Task<PlayerStats> GetPlayerStatsAsync();
    Task<GameSession> GetGameStatsAsync(Guid gameId);
    Task<GameSession?> GetPlayerCompletedDailyGameAsync(Guid playerId, int dailyNumber);
    Task<List<int>> GetPlayerCompletedDailyNumbersAsync(Guid playerId);
}
