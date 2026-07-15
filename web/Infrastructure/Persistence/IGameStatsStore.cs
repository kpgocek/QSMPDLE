using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Infrastructure.Persistence;

public interface IGameStatsStore
{
    Task<GameSession> LoadOrNewAsync(Guid gameId);

    Task SaveAsync(GameSession stats);

    Task<IEnumerable<GameSession>> GetPlayerGames(Guid playerId);

    Task<GlobalStatsView> GetGlobalStatsAsync();

    Task<List<DailyActivePlayersData>> GetDailyActivePlayersAsync(DateOnly? from);

    Task<List<NewVsReturningPlayersData>> GetNewVsReturningPlayersAsync(DateOnly? from);

    Task<PlayerActivityDistributionData[]> GetPlayerActivityDistributionAsync();

    Task<GamesPerPlayerStats> GetGamesPerPlayerStatsAsync();

    Task<RetentionStats> GetRetentionStatsAsync();

    Task<GlobalCharacterStats> GetGlobalCharacterStatsAsync();

    Task<PlayerCharacterStats> GetPlayerCharacterStatsAsync(Guid playerId);
}
