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
    IHiLoScoreStore hiLoScoreStore,
    ICharacterStore characterStore,
    IDayService dayService) : IPlayerProfileService
{
    public async Task<PlayerProfile> GetProfileAsync()
    {
        var playerId = await playerStatsStore.GetPlayerIdAsync();
        var games = (await gameStatsStore.GetPlayerGames(playerId))
            .Where(game => !game.IsLegacyDuplicate)
            .ToList();

        var canonicalGames = games.Where(game => game.SessionCategory == SessionCategory.CanonicalPuzzle).ToList();
        var practiceGames = games.Where(game => game.SessionCategory == SessionCategory.Practice).ToList();
        var finishedPracticeGames = practiceGames.Where(game => game.Outcome != GameOutcome.InProgress).ToList();
        var characterStats = playerId == Guid.Empty ? null : await GetCharacterStatsAsync(canonicalGames);

        return new PlayerProfile
        {
            Stats = PlayerStatsProjection.Create(new PlayerStats { Id = playerId }, games, dayService),
            PracticeGamesStarted = practiceGames.Count,
            PracticeGamesFinished = finishedPracticeGames.Count,
            PracticeGamesWon = finishedPracticeGames.Count(game => game.Outcome == GameOutcome.Won),
            HiLoBestScore = await hiLoScoreStore.GetBestScoreAsync(playerId),
            CharacterStats = characterStats,
        };
    }

    private async Task<PlayerCharacterStats> GetCharacterStatsAsync(List<GameSession> canonicalGames)
    {
        var todayPuzzleId = dayService.GetTodayDayNumber();

        // SQL "PuzzleId <> today" is NULL for a missing PuzzleId, so those sessions never counted either.
        var settledGames = canonicalGames.Where(game => game.PuzzleId is { } puzzleId && puzzleId != todayPuzzleId).ToList();

        // Wrong guesses only (guess != target), grouped by guessed character.
        var mostGuessed = MostFrequent(settledGames.SelectMany(game => game.Guesses
            .Where(guess => guess.GuessedCharacterId != game.TargetCharacterId)
            .Select(guess => guess.GuessedCharacterId)));

        // Won sessions only, grouped by target character.
        var mostCorrect = MostFrequent(settledGames.Where(game => game.Outcome == GameOutcome.Won).Select(game => game.TargetCharacterId));

        var stats = new PlayerCharacterStats
        {
            MostGuessedCharacterId = mostGuessed?.CharacterId,
            MostGuessedCount = mostGuessed?.Count ?? 0,
            MostCorrectlyGuessedCharacterId = mostCorrect?.CharacterId,
            MostCorrectlyGuessedCount = mostCorrect?.Count ?? 0,
        };

        var lookups = await characterStore.GetLookupsAsync();

        var mostGuessedLookup = lookups.FirstOrDefault(lookup => lookup.Id == stats.MostGuessedCharacterId);
        var mostCorrectLookup = lookups.FirstOrDefault(lookup => lookup.Id == stats.MostCorrectlyGuessedCharacterId);

        return stats with
        {
            MostGuessedCharacterName = mostGuessedLookup?.Name ?? stats.MostGuessedCharacterId?.ToString(),
            MostGuessedPortraitFolder = mostGuessedLookup?.PortraitFolder,
            MostCorrectlyGuessedCharacterName = mostCorrectLookup?.Name ?? stats.MostCorrectlyGuessedCharacterId?.ToString(),
            MostCorrectlyGuessedPortraitFolder = mostCorrectLookup?.PortraitFolder,
        };
    }

    // ORDER BY "Count" DESC LIMIT 1; the SQL left ties unordered, so the lowest id wins to stay deterministic.
    private static (int CharacterId, long Count)? MostFrequent(IEnumerable<int> characterIds) =>
        characterIds
            .GroupBy(id => id)
            .Select(group => (CharacterId: group.Key, Count: group.LongCount()))
            .OrderByDescending(entry => entry.Count)
            .ThenBy(entry => entry.CharacterId)
            .Select(entry => ((int CharacterId, long Count)?)entry)
            .FirstOrDefault();
}
