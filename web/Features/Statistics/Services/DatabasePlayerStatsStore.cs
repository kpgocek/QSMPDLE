using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Infrastructure.LocalStorage;
using QSMPDLE.Web.Infrastructure.Persistence;

namespace QSMPDLE.Web.Features.Statistics.Services;

/// <summary>
/// Keeps only the anonymous browser identity locally and derives all statistics
/// from persisted game sessions and guesses.
/// </summary>
public sealed class DatabasePlayerStatsStore(
    LocalStoragePlayerStatsStore localIdentityStore,
    IGameStatsStore gameStatsStore,
    IDayService dayService) : IPlayerStatsStore
{
    public async Task<PlayerStats> LoadAsync()
    {
        var localIdentity = await localIdentityStore.LoadAsync();
        var games = await gameStatsStore.GetPlayerGames(localIdentity.Id, SessionCategory.CanonicalPuzzle);
        return PlayerStatsProjection.Create(localIdentity, games, dayService);
    }

    public Task<Guid> GetPlayerIdAsync() => localIdentityStore.GetPlayerIdAsync();
}

public static class PlayerStatsProjection
{
    public static PlayerStats Create(PlayerStats localIdentity, IEnumerable<GameSession> sessions, IDayService dayService)
    {
        var games = sessions.Where(game => !game.IsLegacyDuplicate).ToList();
        var canonicalGames = games.Where(game => game.SessionCategory == SessionCategory.CanonicalPuzzle).ToList();
        var finishedCanonicalGames = canonicalGames.Where(game => game.Outcome != GameOutcome.InProgress).ToList();
        var wonGames = finishedCanonicalGames.Where(game => game.Outcome == GameOutcome.Won).ToList();
        var dailyGames = canonicalGames.Where(game => game.FirstEntryPoint == EntryPoint.Daily).ToList();
        var streak = CalculateStreak(dailyGames, dayService);

        return new PlayerStats
        {
            Id = localIdentity.Id,
            Version = PlayerStats.CurrentVersion,
            GamesPlayed = finishedCanonicalGames.Count,
            GamesWon = wonGames.Count,
            CurrentStreak = streak.Current,
            MaxStreak = streak.Max,
            GuessDistribution = Enumerable.Range(1, GameRules.MaxGuesses)
                .Select(guessCount => wonGames.Count(game => Math.Clamp(game.Guesses.Count, 1, GameRules.MaxGuesses) == guessCount))
                .ToArray(),
        };
    }

    private static Streak CalculateStreak(IEnumerable<GameSession> dailyGames, IDayService dayService)
    {
        // Needs a saved finish: without one, nothing proves the game was decided on its own day.
        var eligible = dailyGames
            .Where(game => game.FinishedOnUtc.HasValue
                           && game.PuzzleId.HasValue
                           && DateOnly.FromDateTime(game.StartedOnUtc.UtcDateTime) == dayService.GetArchiveDate(game.PuzzleId.Value)
                           && DateOnly.FromDateTime(game.FinishedOnUtc!.Value.UtcDateTime) == dayService.GetArchiveDate(game.PuzzleId.Value))
            .OrderBy(game => game.PuzzleId)
            .ToList();

        var current = 0;
        var max = 0;
        int? previousWinningPuzzle = null;

        foreach (var game in eligible)
        {
            if (game.Outcome != GameOutcome.Won)
            {
                current = 0;
                previousWinningPuzzle = null;
                continue;
            }

            current = previousWinningPuzzle.HasValue && game.PuzzleId == previousWinningPuzzle + 1
                ? current + 1
                : 1;
            previousWinningPuzzle = game.PuzzleId;
            max = Math.Max(max, current);
        }

        // A streak survives only while yesterday's daily was won; today's can still be played.
        if (previousWinningPuzzle < dayService.GetTodayDayNumber() - 1)
            current = 0;

        return new Streak(current, max);
    }

    private sealed record Streak(int Current, int Max);
}
