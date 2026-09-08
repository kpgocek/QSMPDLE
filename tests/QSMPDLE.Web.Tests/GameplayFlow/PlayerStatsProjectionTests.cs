using FluentAssertions;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Features.Statistics.Services;

namespace QSMPDLE.Web.Tests.GameplayFlow;

public sealed class PlayerStatsProjectionTests
{
    [Fact]
    public void DerivesCanonicalStatisticsFromPersistedSessionsAndGuesses()
    {
        var playerId = Guid.NewGuid();
        var firstDay = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var games = new[]
        {
            CreateGame(1, EntryPoint.Daily, firstDay, isWon: true, guesses: 1),
            CreateGame(2, EntryPoint.Daily, firstDay.AddDays(1), isWon: false, guesses: 2),
            CreateGame(3, EntryPoint.Archive, firstDay.AddDays(6), isWon: true, guesses: 3),
            new GameSession { GameId = Guid.NewGuid(), SessionCategory = SessionCategory.Practice, StartedOnUtc = firstDay, FinishedOnUtc = firstDay, IsWon = true },
            new GameSession { GameId = Guid.NewGuid(), PuzzleId = 4, SessionCategory = SessionCategory.CanonicalPuzzle, IsLegacyDuplicate = true, StartedOnUtc = firstDay, FinishedOnUtc = firstDay, IsWon = true },
        };

        var stats = PlayerStatsProjection.Create(new PlayerStats { Id = playerId }, games, new DayService());

        stats.Id.Should().Be(playerId);
        stats.GamesPlayed.Should().Be(3);
        stats.GamesWon.Should().Be(2);
        stats.ArchiveGamesPlayed.Should().Be(1);
        stats.ArchiveGamesWon.Should().Be(1);
        stats.GuessDistribution.Should().Equal(1, 1, 1, 0, 0, 0);
        stats.CurrentStreak.Should().Be(0);
        stats.MaxStreak.Should().Be(1);
        stats.LastCompletedDayNumber.Should().Be(1);
    }

    private static GameSession CreateGame(int puzzleId, EntryPoint entryPoint, DateTimeOffset startedOnUtc, bool isWon, int guesses)
    {
        var game = new GameSession
        {
            GameId = Guid.NewGuid(),
            PuzzleId = puzzleId,
            SessionCategory = SessionCategory.CanonicalPuzzle,
            FirstEntryPoint = entryPoint,
            StartedOnUtc = startedOnUtc,
            FinishedOnUtc = startedOnUtc.AddMinutes(3),
            IsWon = isWon,
        };

        for (var index = 0; index < guesses; index++)
            game.AddGuess(index + 1);

        return game;
    }
}
