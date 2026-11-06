using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Infrastructure.LocalStorage;
using QSMPDLE.Web.Infrastructure.Persistence;

namespace QSMPDLE.Web.Features.Statistics.Services;

/// <summary>
/// Composes the profile dialog from a single read of the player's history. The identity comes from
/// <see cref="IPlayerStatsStore.GetPlayerIdAsync"/> because LoadAsync would read that history again.
/// </summary>
public sealed class PlayerProfileService(
    IPlayerStatsStore playerStatsStore,
    IGameStatsStore gameStatsStore,
    IGlobalStatsService globalStatsService,
    ICharacterStore characterStore,
    IDayService dayService) : IPlayerProfileService
{
    private const int GuessDistributionSize = 6;

    public async Task<PlayerProfile> GetProfileAsync()
    {
        var playerId = await playerStatsStore.GetPlayerIdAsync();
        var games = (await gameStatsStore.GetPlayerGames(playerId))
            .Where(game => !game.IsLegacyDuplicate)
            .ToList();

        var canonicalGames = games.Where(game => game.SessionCategory == SessionCategory.CanonicalPuzzle).ToList();
        var practiceGames = games.Where(game => game.SessionCategory == SessionCategory.Practice).ToList();
        var finishedPracticeGames = practiceGames.Where(game => game.FinishedOnUtc is not null).ToList();
        var dailyGamesWon = canonicalGames.Count(game => game.IsWon);

        var memberSince = await gameStatsStore.GetPlayerFirstGameDateAsync(playerId);
        var globalStats = await globalStatsService.GetGlobalStatsAsync();
        var characterStats = playerId == Guid.Empty ? null : await GetCharacterStatsAsync(playerId);

        return new PlayerProfile
        {
            Stats = PlayerStatsProjection.Create(new PlayerStats { Id = playerId }, games, dayService),
            DailyGamesPlayed = canonicalGames.Count,
            DailyGamesWon = dailyGamesWon,
            DailyWinRate = canonicalGames.Count == 0 ? 0 : dailyGamesWon * 100d / canonicalGames.Count,
            GuessDistribution = Enumerable.Range(1, GuessDistributionSize)
                .Select(count => canonicalGames.Count(game => game.IsWon && game.Guesses.Count == count))
                .ToArray(),
            PracticeGamesStarted = practiceGames.Count,
            PracticeGamesFinished = finishedPracticeGames.Count,
            PracticeGamesWon = finishedPracticeGames.Count(game => game.IsWon),
            MemberSince = memberSince,
            GlobalStats = globalStats,
            CharacterStats = characterStats,
        };
    }

    private async Task<PlayerCharacterStats> GetCharacterStatsAsync(Guid playerId)
    {
        var stats = await gameStatsStore.GetPlayerCharacterStatsAsync(playerId);
        var lookups = await characterStore.GetLookupsAsync();

        var mostGuessed = lookups.FirstOrDefault(lookup => lookup.Id == stats.MostGuessedCharacterId);
        var mostCorrect = lookups.FirstOrDefault(lookup => lookup.Id == stats.MostCorrectlyGuessedCharacterId);

        return stats with
        {
            MostGuessedCharacterName = mostGuessed?.Name ?? stats.MostGuessedCharacterId?.ToString(),
            MostGuessedIconUrl = mostGuessed?.CharacterIconUrl,
            MostCorrectlyGuessedCharacterName = mostCorrect?.Name ?? stats.MostCorrectlyGuessedCharacterId?.ToString(),
            MostCorrectlyGuessedIconUrl = mostCorrect?.CharacterIconUrl,
        };
    }
}
