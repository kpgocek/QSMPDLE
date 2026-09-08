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
        var games = await gameStatsStore.GetPlayerGames(localIdentity.Id);
        return PlayerStatsProjection.Create(localIdentity, games, dayService);
    }

    public Task SaveAsync(PlayerStats stats) => localIdentityStore.SaveAsync(stats);

    public Task ClearAsync() => localIdentityStore.ClearAsync();
}

public static class PlayerStatsProjection
{
    private const int GuessDistributionSize = 6;

    public static PlayerStats Create(PlayerStats localIdentity, IEnumerable<GameSession> sessions, IDayService dayService)
    {
        var games = sessions.Where(game => !game.IsLegacyDuplicate).ToList();
        var canonicalGames = games.Where(game => game.SessionCategory == SessionCategory.CanonicalPuzzle).ToList();
        var finishedCanonicalGames = canonicalGames.Where(game => game.FinishedOnUtc.HasValue).ToList();
        var finishedArchiveGames = finishedCanonicalGames.Where(game => game.FirstEntryPoint == EntryPoint.Archive).ToList();
        var dailyGames = canonicalGames.Where(game => game.FirstEntryPoint == EntryPoint.Daily).ToList();
        var streak = CalculateStreak(dailyGames, dayService);

        return new PlayerStats
        {
            Id = localIdentity.Id,
            Version = PlayerStats.CurrentVersion,
            GamesPlayed = finishedCanonicalGames.Count,
            GamesWon = finishedCanonicalGames.Count(game => game.IsWon),
            ArchiveGamesPlayed = finishedArchiveGames.Count,
            ArchiveGamesWon = finishedArchiveGames.Count(game => game.IsWon),
            ArchiveGamesLost = finishedArchiveGames.Count(game => !game.IsWon),
            CurrentStreak = streak.Current,
            MaxStreak = streak.Max,
            LastCompletedDayNumber = streak.LastCompletedPuzzleId,
            LastPlayedDailyGameId = dailyGames.OrderByDescending(game => game.StartedOnUtc).Select(game => game.GameId).FirstOrDefault(),
            GuessDistribution = Enumerable.Range(1, GuessDistributionSize)
                .Select(guessCount => finishedCanonicalGames.Count(game => Math.Clamp(game.Guesses.Count, 1, GuessDistributionSize) == guessCount))
                .ToArray(),
        };
    }

    private static Streak CalculateStreak(IEnumerable<GameSession> dailyGames, IDayService dayService)
    {
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
        int? lastCompletedPuzzle = null;

        foreach (var game in eligible)
        {
            if (!game.IsWon)
            {
                current = 0;
                previousWinningPuzzle = null;
                continue;
            }

            current = previousWinningPuzzle.HasValue && game.PuzzleId == previousWinningPuzzle + 1
                ? current + 1
                : 1;
            previousWinningPuzzle = game.PuzzleId;
            lastCompletedPuzzle = game.PuzzleId;
            max = Math.Max(max, current);
        }

        return new Streak(current, max, lastCompletedPuzzle);
    }

    private sealed record Streak(int Current, int Max, int? LastCompletedPuzzleId);
}
