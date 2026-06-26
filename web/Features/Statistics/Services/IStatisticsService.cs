using QSMPDLE.Web.Features.Communication.GameEvents;
using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Features.Statistics.Services;

public interface IStatisticsService
{
    // EVENTS
    Task RecordGameStartedAsync(GameStartedEvent eventData);
    Task RecordGuessMadeAsync(GuessMadeEvent eventData);
    Task RecordGameFinishedAsync(GameFinishedEvent eventData);


    // ENDPOINTS
    Task<PlayerStats> GetPlayerStatsAsync();
    Task<GameSession> GetGameStatsAsync(Guid gameId);
}
