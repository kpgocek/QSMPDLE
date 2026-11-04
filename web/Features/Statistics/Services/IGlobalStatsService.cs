using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Features.Statistics.Services;

/// <summary>Site-wide statistics shared by every visitor; never holds per-player data.</summary>
public interface IGlobalStatsService
{
    Task<GlobalStatsView> GetGlobalStatsAsync();

    Task<List<DailyActivePlayersData>> GetDailyActivePlayersAsync(DateOnly? from);

    Task<List<NewVsReturningPlayersData>> GetNewVsReturningPlayersAsync(DateOnly? from);

    Task<PlayerActivityDistributionData[]> GetPlayerActivityDistributionAsync();

    Task<GamesPerPlayerStats> GetGamesPerPlayerStatsAsync();

    Task<RetentionStats> GetRetentionStatsAsync();

    Task<GlobalCharacterStats> GetGlobalCharacterStatsAsync();
}
