using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Infrastructure.Persistence;

public interface IGameStatsStore
{
    Task<GameSession> LoadOrNewAsync(Guid gameId);

    Task SaveAsync(GameSession stats);

    Task<IEnumerable<GameSession>> GetPlayerGames(Guid playerId);

    Task<GlobalStatsView> GetGlobalStatsAsync();
}
