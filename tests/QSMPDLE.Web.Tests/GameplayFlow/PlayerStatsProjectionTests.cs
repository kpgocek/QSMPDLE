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
        stats.GuessDistribution.Should().Equal([1, 0, 1, 0, 0, 0], "a loss is not a result in two guesses");
        stats.CurrentStreak.Should().Be(0);
        stats.MaxStreak.Should().Be(1);
    }

    private static readonly DateTimeOffset FirstDay = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Streak_ArchiveReplayOfMissedDay_DoesNotBridgeTheGap()
    {
        var games = new[]
        {
            CreateGame(1, EntryPoint.Daily, FirstDay, isWon: true, guesses: 1),
            CreateGame(2, EntryPoint.Daily, FirstDay.AddDays(1), isWon: true, guesses: 1),
            CreateGame(3, EntryPoint.Archive, FirstDay.AddDays(5), isWon: true, guesses: 1),
            CreateGame(4, EntryPoint.Daily, FirstDay.AddDays(3), isWon: true, guesses: 1),
        };

        var stats = PlayerStatsProjection.Create(new PlayerStats(), games, OnPuzzleDay(4));

        stats.CurrentStreak.Should().Be(1, "day 3 was only played later from the archive");
        stats.MaxStreak.Should().Be(2);
    }

    [Fact]
    public void Streak_PracticeWins_NeverCount()
    {
        // Practice sessions default to a Daily entry point, so only their category keeps them out.
        var games = new[]
        {
            CreateGame(1, EntryPoint.Daily, FirstDay, isWon: true, guesses: 1),
            new GameSession { GameId = Guid.NewGuid(), SessionCategory = SessionCategory.Practice, StartedOnUtc = FirstDay.AddDays(1), FinishedOnUtc = FirstDay.AddDays(1), IsWon = true },
            new GameSession { GameId = Guid.NewGuid(), SessionCategory = SessionCategory.Practice, StartedOnUtc = FirstDay.AddDays(2), FinishedOnUtc = FirstDay.AddDays(2), IsWon = true },
        };

        var stats = PlayerStatsProjection.Create(new PlayerStats(), games, OnPuzzleDay(2));

        stats.CurrentStreak.Should().Be(1);
        stats.MaxStreak.Should().Be(1);
    }

    [Fact]
    public void Streak_DailyFinishedAfterItsDay_DoesNotCount()
    {
        // Opened on Daily and first guessed on the day, but finished the next day (e.g. from the archive).
        var late = CreateGame(2, EntryPoint.Daily, FirstDay.AddDays(1), isWon: true, guesses: 1);
        late.FinishedOnUtc = FirstDay.AddDays(2);
        var games = new[]
        {
            CreateGame(1, EntryPoint.Daily, FirstDay, isWon: true, guesses: 1),
            late,
            CreateGame(3, EntryPoint.Daily, FirstDay.AddDays(2), isWon: true, guesses: 1),
        };

        var stats = PlayerStatsProjection.Create(new PlayerStats(), games, OnPuzzleDay(3));

        stats.CurrentStreak.Should().Be(1);
        stats.MaxStreak.Should().Be(1);
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(4, 3)]
    [InlineData(5, 0)]
    [InlineData(30, 0)]
    public void Streak_DaysSkippedAfterLastWin_ResetOnceYesterdayIsMissed(int today, int expectedCurrent)
    {
        // Won puzzles 1-3 on their days; today's puzzle may still be played, a missed yesterday ends the run.
        var games = Enumerable.Range(1, 3).Select(day => CreateGame(day, EntryPoint.Daily, FirstDay.AddDays(day - 1), isWon: true, guesses: 1)).ToArray();

        var stats = PlayerStatsProjection.Create(new PlayerStats(), games, OnPuzzleDay(today));

        stats.CurrentStreak.Should().Be(expectedCurrent);
        stats.MaxStreak.Should().Be(3, "the best run is history and never resets");
    }

    [Fact]
    public void Create_GamesDecidedByTheirGuesses_CountWithoutASavedFinish()
    {
        var lost = CreateGame(1, EntryPoint.Archive, FirstDay, isWon: false, guesses: 6);
        var won = CreateGame(2, EntryPoint.Archive, FirstDay, isWon: false, guesses: 1);
        var open = CreateGame(3, EntryPoint.Archive, FirstDay, isWon: false, guesses: 5);
        foreach (var game in new[] { lost, won, open })
        {
            game.TargetCharacterId = 100;
            game.FinishedOnUtc = null;
        }
        won.AddGuess(100);

        var stats = PlayerStatsProjection.Create(new PlayerStats(), [lost, won, open], OnPuzzleDay(30));

        stats.GamesPlayed.Should().Be(2);
        stats.GamesWon.Should().Be(1);
    }

    private static DayService OnPuzzleDay(int puzzleId) => new(new TestClock(FirstDay.AddDays(puzzleId - 1)));

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
