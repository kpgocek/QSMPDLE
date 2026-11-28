using FluentAssertions;
using QSMPDLE.Web.Extensions;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Features.Statistics.Services;

namespace QSMPDLE.Web.Tests.GameplayFlow;

public sealed class GameStateManagerStatisticsTests
{
    [Fact]
    public async Task GameStartedStatisticIsRecordedAtTheFirstValidGuessPoint()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Daily);

        await setup.Manager.MakeGuessAsync(setup.IncorrectGuess.Id);

        setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId].StartedOnUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task ValidGuessProducesExpectedPersistedStatisticsState()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Daily);

        await setup.Manager.MakeGuessAsync(setup.IncorrectGuess.Id);

        var session = setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId];
        session.Guesses.Should().ContainSingle();
        session.Guesses.First().GuessedCharacterId.Should().Be(setup.IncorrectGuess.Id);
    }

    [Fact]
    public async Task WinningCompletionIsRecorded()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Daily);

        await setup.Manager.MakeGuessAsync(setup.Target.Id);

        var session = setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId];
        session.FinishedOnUtc.Should().NotBeNull();
        session.IsWon.Should().BeTrue();
        setup.ProjectedStats().GamesWon.Should().Be(1);
        setup.ProjectedStats().GamesPlayed.Should().Be(1);
    }

    [Fact]
    public async Task LosingCompletionIsRecorded()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Daily);

        foreach (var guess in setup.LosingGuesses)
        {
            await setup.Manager.MakeGuessAsync(guess.Id);
        }

        var session = setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId];
        session.FinishedOnUtc.Should().NotBeNull();
        session.IsWon.Should().BeFalse();
        session.Guesses.Should().HaveCount(6);
        setup.ProjectedStats().GamesPlayed.Should().Be(1);
        setup.ProjectedStats().GamesWon.Should().Be(0);
    }

    [Fact]
    public async Task UnfinishedGameDoesNotProduceCompletionStatistics()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Daily);

        await setup.Manager.MakeGuessAsync(setup.IncorrectGuess.Id);

        setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId].FinishedOnUtc.Should().BeNull();
        setup.ProjectedStats().GamesPlayed.Should().Be(0);
    }

    [Fact]
    public async Task DuplicateRejectedGuessDoesNotProduceAdditionalGuessStatistics()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Daily);

        await setup.Manager.MakeGuessAsync(setup.IncorrectGuess.Id);
        await setup.Manager.MakeGuessAsync(setup.IncorrectGuess.Id);

        setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId].Guesses.Should().ContainSingle();
    }

    [Fact]
    public async Task DailyGame_ResolvesPlayerId_WithoutLoadingPlayerHistory()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Daily);

        foreach (var guess in setup.LosingGuesses)
        {
            await setup.Manager.MakeGuessAsync(guess.Id);
        }

        // LoadAsync projects the full session history in production; page loads and guesses must not need it.
        setup.PlayerStatsStore.LoadCount.Should().Be(0);
    }

    [Fact]
    public async Task MakeGuessAsync_DatabaseFailsInDaily_RefusesTheGuessAndCanBeRetried()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Daily);
        setup.GameStatsStore.FailuresLeft = 1;

        var act = () => setup.Manager.MakeGuessAsync(setup.IncorrectGuess.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
        setup.Manager.GameState.GuessesMade.Should().BeEmpty();
        setup.GameStateStore.State!.GuessesMade.Should().BeEmpty();

        var retried = await setup.Manager.MakeGuessAsync(setup.IncorrectGuess.Id);

        retried.Should().NotBeNull();
        setup.GameStatsStore.Sessions.Should().ContainSingle().Which.Value.GuessedCharacterIds.Should()
            .Equal(setup.IncorrectGuess.Id);
    }

    [Fact]
    public async Task MakeGuessAsync_DatabaseFailsInPractice_ContinuesAndTheNextGuessFillsTheGap()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Practice);
        setup.GameStatsStore.FailuresLeft = 1;

        var unrecorded = await setup.Manager.MakeGuessAsync(setup.IncorrectGuess.Id);
        setup.GameStatsStore.Sessions.Should().BeEmpty();
        await setup.Manager.MakeGuessAsync(setup.Target.Id);

        unrecorded.Should().NotBeNull("only statistics depend on recording a practice guess");
        var session = setup.GameStatsStore.Sessions.Should().ContainSingle().Which.Value;
        session.GuessedCharacterIds.Should().Equal(setup.IncorrectGuess.Id, setup.Target.Id);
        session.Guesses.Single(guess => guess.GuessOrder == 0).GuessedOnUtc.Should().BeNull("its real time is unknown");
        session.IsWon.Should().BeTrue();
    }

    [Fact]
    public async Task MakeGuessAsync_AnotherTabGuessedAhead_AdoptsTheRecordedBoard()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Daily);
        await setup.Manager.MakeGuessAsync(setup.IncorrectGuess.Id);
        var session = setup.GameStatsStore.Sessions.Values.Single();
        session.RecordGuesses([setup.IncorrectGuess.Id, setup.LosingGuesses[1].Id], DateTimeOffset.UtcNow);

        var result = await setup.Manager.MakeGuessAsync(setup.LosingGuesses[2].Id);

        result.Should().BeNull();
        setup.Manager.GameState.GuessesMade.Select(guess => guess.Character.Id)
            .Should().Equal(setup.IncorrectGuess.Id, setup.LosingGuesses[1].Id);
        session.Guesses.Should().HaveCount(2);
    }

    [Fact]
    public async Task StartGameAsync_ReloadsMidGame_KeepsOneSessionAndItsStart()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Daily);
        await setup.Manager.MakeGuessAsync(setup.IncorrectGuess.Id);
        var started = setup.GameStatsStore.Sessions.Values.Single().StartedOnUtc;

        for (var reload = 0; reload < 3; reload++)
        {
            await setup.Manager.StartGameAsync(EntryPoint.Daily);
        }

        await setup.Manager.MakeGuessAsync(setup.Target.Id);

        var session = setup.GameStatsStore.Sessions.Should().ContainSingle().Which.Value;
        session.StartedOnUtc.Should().Be(started);
        session.Guesses.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(EntryPoint.Daily)]
    [InlineData(EntryPoint.Practice)]
    public async Task StartGameAsync_NoGuess_RecordsNothing(EntryPoint entryPoint)
    {
        var setup = CreateSetup();

        await setup.Manager.StartGameAsync(entryPoint);
        await setup.Manager.StartNewPracticeGameAsync();

        setup.GameStatsStore.Sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task StartGameAsync_FinishWasNeverSaved_RestoresTheDecidedGame()
    {
        var setup = CreateSetup();
        var lost = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = setup.PlayerStatsStore.Stats.Id,
            PuzzleId = 3,
            TargetCharacterId = setup.Target.Id,
            Guesses =
            [
                .. Enumerable.Range(0, 6).Select(index =>
                    new GameGuess { GuessOrder = index, GuessedCharacterId = setup.LosingGuesses[index].Id })
            ]
        };
        setup.GameStatsStore.Sessions[lost.GameId] = lost;

        await setup.Manager.StartGameAsync(EntryPoint.Archive, 3);

        setup.Manager.GameState.IsLost.Should().BeTrue();
        (await setup.Manager.MakeGuessAsync(setup.LosingGuesses[6].Id)).Should().BeNull();
        lost.Guesses.Should().HaveCount(6);
    }

    [Fact]
    public async Task DailyPlayerStatsAreUpdatedAccordingToCurrentSemantics()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Daily);

        await setup.Manager.MakeGuessAsync(setup.Target.Id);

        setup.ProjectedStats().GamesPlayed.Should().Be(1);
        setup.ProjectedStats().GamesWon.Should().Be(1);
        setup.ProjectedStats().GuessDistribution[0].Should().Be(1);
    }

    [Fact]
    public async Task GameSessionContainsStartFinishWinLossAndGuesses()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Daily);

        await setup.Manager.MakeGuessAsync(setup.Target.Id);

        var session = setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId];
        var dayNumber = setup.Manager.GameState.Game.PuzzleId;

        session.PlayerId.Should().Be(setup.PlayerStatsStore.Stats.Id);
        session.PuzzleId.Should().Be(dayNumber);
        session.TargetCharacterId.Should().Be(setup.Target.Id);
        session.FinishedOnUtc.Should().NotBeNull();
        session.Guesses.Should().ContainSingle();
        session.Guesses.First().GuessedCharacterId.Should().Be(setup.Target.Id);
    }

    [Fact]
    public async Task DailyAndArchiveUseTheSamePlayerIdWhenPlayerStatsAreIntact()
    {
        var setup = CreateSetup();

        await setup.Manager.StartGameAsync(EntryPoint.Daily);
        setup.Manager.GameState.PlayerId.Should().Be(setup.PlayerStatsStore.Stats.Id);

        await setup.Manager.MakeGuessAsync(setup.Target.Id);

        var session = setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId];
        session.PlayerId.Should().Be(setup.PlayerStatsStore.Stats.Id);

        await setup.Manager.StartGameAsync(EntryPoint.Archive, setup.Manager.GameState.Game.PuzzleId);

        setup.Manager.GameState.PlayerId.Should().Be(setup.PlayerStatsStore.Stats.Id);
    }

    // A player's finished canonical puzzles, as the profile and archive calendar read them from the recorded sessions.
    private static List<int> CompletedPuzzles(InMemoryGameStatsStore store, Guid playerId) => store.Sessions.Values
        .Where(s => s.PlayerId == playerId && s.SessionCategory == SessionCategory.CanonicalPuzzle &&
                    !s.IsLegacyDuplicate && s.FinishedOnUtc.HasValue && s.PuzzleId.HasValue)
        .Select(s => s.PuzzleId!.Value)
        .Distinct()
        .ToList();

    [Fact]
    public async Task CompletedDailyWinAppearsInHistoryForSamePlayerId()
    {
        var setup = CreateSetup();

        await setup.Manager.StartGameAsync(EntryPoint.Daily);
        await setup.Manager.MakeGuessAsync(setup.Target.Id);

        var completedDay = setup.Manager.GameState.Game.PuzzleId;
        completedDay.Should().NotBeNull();
        var completedDayValue = completedDay.Value;
        var numbers = CompletedPuzzles(setup.GameStatsStore, setup.PlayerStatsStore.Stats.Id);

        numbers.Should().ContainSingle().Which.Should().Be(completedDayValue);
    }

    [Fact]
    public async Task CompletedDailyLossAppearsInHistoryForSamePlayerId()
    {
        var setup = CreateSetup();

        await setup.Manager.StartGameAsync(EntryPoint.Daily);

        foreach (var guess in setup.LosingGuesses)
        {
            await setup.Manager.MakeGuessAsync(guess.Id);
        }

        var completedDay = setup.Manager.GameState.Game.PuzzleId;
        completedDay.Should().NotBeNull();
        var completedDayValue = completedDay.Value;
        var numbers = CompletedPuzzles(setup.GameStatsStore, setup.PlayerStatsStore.Stats.Id);

        numbers.Should().ContainSingle().Which.Should().Be(completedDayValue);
    }

    [Fact]
    public async Task UnfinishedDailyDoesNotAppearInHistory()
    {
        var setup = CreateSetup();

        await setup.Manager.StartGameAsync(EntryPoint.Daily);
        await setup.Manager.MakeGuessAsync(setup.IncorrectGuess.Id);

        var completedDay = setup.Manager.GameState.Game.PuzzleId;
        completedDay.Should().NotBeNull();
        var completedDayValue = completedDay.Value;
        var numbers = CompletedPuzzles(setup.GameStatsStore, setup.PlayerStatsStore.Stats.Id);

        numbers.Should().BeEmpty();
    }

    [Fact]
    public async Task DifferentPlayerIdDoesNotSeeAnotherPlayersHistory()
    {
        var setup = CreateSetup();
        var playerId = setup.PlayerStatsStore.Stats.Id;

        await setup.Manager.StartGameAsync(EntryPoint.Daily);
        await setup.Manager.MakeGuessAsync(setup.Target.Id);

        var otherPlayerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        setup.PlayerStatsStore.Stats = new PlayerStats { Id = otherPlayerId };
        var completedDayValue = setup.Manager.GameState.Game.PuzzleId!.Value;

        CompletedPuzzles(setup.GameStatsStore, otherPlayerId).Should().BeEmpty();
        CompletedPuzzles(setup.GameStatsStore, playerId).Should().ContainSingle();
    }

    [Fact]
    public async Task ArchiveDoesNotChangeDailyStreakOrProgression()
    {
        var setup = CreateSetup();

        await setup.Manager.StartGameAsync(EntryPoint.Daily);
        await setup.Manager.MakeGuessAsync(setup.Target.Id);

        var beforeStats = new PlayerStats
        {
            Version = setup.PlayerStatsStore.Stats.Version,
            Id = setup.PlayerStatsStore.Stats.Id,
            GamesPlayed = setup.ProjectedStats().GamesPlayed,
            GamesWon = setup.ProjectedStats().GamesWon,
            CurrentStreak = setup.ProjectedStats().CurrentStreak,
            MaxStreak = setup.ProjectedStats().MaxStreak,
            GuessDistribution = setup.ProjectedStats().GuessDistribution.ToArray()
        };
        await setup.Manager.StartGameAsync(EntryPoint.Archive, setup.Manager.GameState.Game.PuzzleId!.Value);

        setup.ProjectedStats().GamesPlayed.Should().Be(beforeStats.GamesPlayed);
        setup.ProjectedStats().GamesWon.Should().Be(beforeStats.GamesWon);
        setup.ProjectedStats().CurrentStreak.Should().Be(beforeStats.CurrentStreak);
    }

    [Fact]
    public async Task ArchiveRecognizesCompletedDailyWin()
    {
        var setup = CreateSetup();
        var completedDaily = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = setup.PlayerStatsStore.Stats.Id,
            PuzzleId = 3,
            TargetCharacterId = setup.Target.Id,
            FinishedOnUtc = DateTimeOffset.UtcNow,
            IsWon = true,
            Guesses =
            [
                new GameGuess { GameId = Guid.NewGuid(), GuessOrder = 0, GuessedCharacterId = setup.Target.Id }
            ]
        };
        setup.GameStatsStore.Sessions[completedDaily.GameId] = completedDaily;

        var result = await setup.Manager.StartGameAsync(EntryPoint.Archive, 3);

        result.Should().Be(LoadGameResult.LoadedExisting);
        setup.Manager.GameState.EntryPoint.Should().Be(EntryPoint.Archive);
        setup.Manager.GameState.GameId.Should().NotBe(Guid.Empty);
        setup.Manager.GameState.IsWon.Should().BeTrue();
        setup.Manager.GameState.IsFinished.Should().BeTrue();
        setup.Manager.GameState.Game.PuzzleId.Should().Be(3);
    }

    [Fact]
    public async Task ArchiveRecognizesCompletedDailyLoss()
    {
        var setup = CreateSetup();
        var completedDaily = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = setup.PlayerStatsStore.Stats.Id,
            PuzzleId = 3,
            TargetCharacterId = setup.Target.Id,
            FinishedOnUtc = DateTimeOffset.UtcNow,
            IsWon = false,
            Guesses = Enumerable.Range(0, 6).Select(index => new GameGuess
            {
                GameId = Guid.NewGuid(), GuessOrder = index, GuessedCharacterId = setup.LosingGuesses[index].Id
            }).ToList()
        };
        setup.GameStatsStore.Sessions[completedDaily.GameId] = completedDaily;

        var result = await setup.Manager.StartGameAsync(EntryPoint.Archive, 3);

        result.Should().Be(LoadGameResult.LoadedExisting);
        setup.Manager.GameState.EntryPoint.Should().Be(EntryPoint.Archive);
        setup.Manager.GameState.IsLost.Should().BeTrue();
        setup.Manager.GameState.IsFinished.Should().BeTrue();
    }

    [Fact]
    public async Task ArchiveRemainsPlayableWhenDailyIsUnfinished()
    {
        var setup = CreateSetup();
        var unfinishedDaily = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = setup.PlayerStatsStore.Stats.Id,
            PuzzleId = 3,
            TargetCharacterId = setup.Target.Id,
            StartedOnUtc = DateTimeOffset.UtcNow,
            Guesses =
            [
                new GameGuess
                {
                    GameId = Guid.NewGuid(), GuessOrder = 0, GuessedCharacterId = setup.IncorrectGuess.Id
                }
            ]
        };
        setup.GameStatsStore.Sessions[unfinishedDaily.GameId] = unfinishedDaily;

        var result = await setup.Manager.StartGameAsync(EntryPoint.Archive, 3);

        result.Should().Be(LoadGameResult.LoadedExisting);
        setup.Manager.GameState.EntryPoint.Should().Be(EntryPoint.Archive);
        setup.Manager.GameState.IsFinished.Should().BeFalse();
    }

    [Fact]
    public async Task ArchiveRemainsPlayableWithoutDailyHistory()
    {
        var setup = CreateSetup();

        var result = await setup.Manager.StartGameAsync(EntryPoint.Archive, 3);

        result.Should().Be(LoadGameResult.CreatedNew);
        setup.Manager.GameState.EntryPoint.Should().Be(EntryPoint.Archive);
        setup.Manager.GameState.IsFinished.Should().BeFalse();
    }

    [Fact]
    public async Task ArchiveIgnoresOtherPlayersCompletedDaily()
    {
        var setup = CreateSetup();
        var otherPlayerSession = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            PuzzleId = 3,
            TargetCharacterId = setup.Target.Id,
            FinishedOnUtc = DateTimeOffset.UtcNow,
            IsWon = true
        };
        setup.GameStatsStore.Sessions[otherPlayerSession.GameId] = otherPlayerSession;

        var result = await setup.Manager.StartGameAsync(EntryPoint.Archive, 3);

        result.Should().Be(LoadGameResult.CreatedNew);
        setup.Manager.GameState.EntryPoint.Should().Be(EntryPoint.Archive);
        setup.Manager.GameState.IsFinished.Should().BeFalse();
    }

    [Fact]
    public async Task ArchiveKeepsExistingLocalStateWhenPresent()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Archive, 3);
        var firstGameId = setup.Manager.GameState.GameId;

        var result = await setup.Manager.StartGameAsync(EntryPoint.Archive, 3);

        result.Should().Be(LoadGameResult.LoadedExisting);
        setup.Manager.GameState.GameId.Should().Be(firstGameId);
    }

    [Fact]
    public async Task ArchiveRejectsCachedStateWithDifferentDay()
    {
        var setup = CreateSetup();

        await setup.Manager.StartGameAsync(EntryPoint.Archive, 3);

        setup.GameStateStore.State = new GameState
        {
            GameId = Guid.NewGuid(),
            PlayerId = setup.PlayerStatsStore.Stats.Id,
            Game = new Game { PuzzleId = 5, TargetId = setup.Target.Id, PortraitUrl = string.Empty }
        };

        var result = await setup.Manager.StartGameAsync(EntryPoint.Archive, 3);

        result.Should().Be(LoadGameResult.CreatedNew);
        setup.Manager.GameState.Game.PuzzleId.Should().Be(3);
    }

    [Fact]
    public async Task ArchiveDoesNotChangeDailyStreakWhenLoadingCompletedDaily()
    {
        var setup = CreateSetup();
        var completedDaily = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = setup.PlayerStatsStore.Stats.Id,
            PuzzleId = 3,
            TargetCharacterId = setup.Target.Id,
            FinishedOnUtc = DateTimeOffset.UtcNow,
            IsWon = true,
            Guesses =
            [
                new GameGuess { GameId = Guid.NewGuid(), GuessOrder = 0, GuessedCharacterId = setup.Target.Id }
            ]
        };
        setup.GameStatsStore.Sessions[completedDaily.GameId] = completedDaily;

        var beforeStreak = setup.ProjectedStats().CurrentStreak;
        var beforeGamesPlayed = setup.ProjectedStats().GamesPlayed;

        await setup.Manager.StartGameAsync(EntryPoint.Archive, 3);

        setup.ProjectedStats().CurrentStreak.Should().Be(beforeStreak);
        setup.ProjectedStats().GamesPlayed.Should().Be(beforeGamesPlayed);
    }

    [Fact]
    public async Task CompletedGamesIncludeLossesInAverageGuessCount()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Daily);

        foreach (var guess in setup.LosingGuesses)
        {
            await setup.Manager.MakeGuessAsync(guess.Id);
        }

        var session = setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId];
        session.FinishedOnUtc.Should().NotBeNull();
        session.Guesses.Should().HaveCount(6);
    }

    [Fact]
    public async Task MakeGuessAsync_CanonicalGame_RecordsStartGuessAndCompletionInGameStatsStore()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Archive, 3);

        await setup.Manager.MakeGuessAsync(setup.IncorrectGuess.Id);
        await setup.Manager.MakeGuessAsync(setup.Target.Id);

        var session = setup.GameStatsStore.Sessions.Should().ContainSingle().Which.Value;
        session.GameId.Should().Be(setup.Manager.GameState.GameId);
        session.PlayerId.Should().Be(setup.PlayerStatsStore.Stats.Id);
        session.StartedOnUtc.Should().NotBe(default);
        session.PuzzleId.Should().Be(3);
        session.TargetCharacterId.Should().Be(setup.Target.Id);
        session.SessionCategory.Should().Be(SessionCategory.CanonicalPuzzle);
        session.FirstEntryPoint.Should().Be(EntryPoint.Archive);
        session.Guesses.Select(guess => guess.GuessedCharacterId).Should()
            .Equal(setup.IncorrectGuess.Id, setup.Target.Id);
        session.FinishedOnUtc.Should().NotBeNull();
        session.IsWon.Should().BeTrue();
    }

    [Fact]
    public async Task MakeGuessAsync_EmptyPlayerId_ThrowsBeforeRecordingAnything()
    {
        var setup = CreateSetup();
        // Practice skips the canonical claim, so the recording guard is the first store write.
        await setup.Manager.StartGameAsync(EntryPoint.Practice);
        setup.Manager.GameState.PlayerId = Guid.Empty;

        var act = () => setup.Manager.MakeGuessAsync(setup.IncorrectGuess.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot record telemetry without a valid player id.");
        setup.GameStatsStore.Sessions.Should().BeEmpty();
    }

    private static Setup CreateSetup()
    {
        var target = TestCharacters.Create(1, "Target", 10, 2, ["Any"], ["Guild"], ["Human"]);
        var incorrectGuess = TestCharacters.Create(2, "IncorrectGuess", 20, 4, ["He/Him"], ["Other"], ["Unknown"]);
        var lose4 = TestCharacters.Create(4, "Lose4", 40, 1, ["They/Them"], ["Other"], ["Human"]);
        var lose5 = TestCharacters.Create(5, "Lose5", 50, 2, ["Any"], ["Other"], ["Human"]);
        var lose6 = TestCharacters.Create(6, "Lose6", 60, 3, ["He/Him"], ["Other"], ["Human"]);
        var lose7 = TestCharacters.Create(7, "Lose7", 70, 4, ["She/Her"], ["Other"], ["Human"]);
        var lose8 = TestCharacters.Create(8, "Lose8", 80, 1, ["They/Them"], ["Other"], ["Human"]);
        var lose9 = TestCharacters.Create(9, "Lose9", 90, 2, ["Any"], ["Other"], ["Human"]);
        var characterStore = new InMemoryCharacterStore(
            [target, incorrectGuess, lose4, lose5, lose6, lose7, lose8, lose9], target.Id,
            new Dictionary<int, int> { [3] = target.Id });
        var playerStatsStore = new InMemoryPlayerStatsStore
        {
            Stats = new PlayerStats { Id = Guid.Parse("11111111-1111-1111-1111-111111111111") }
        };
        var gameStateStore = new InMemoryGameStateStore();
        var gameStatsStore = new InMemoryGameStatsStore();
        var dayService = new DayService();
        var manager = new GameStateManager(gameStateStore, new GameService(characterStore, dayService), dayService,
            playerStatsStore, characterStore, new CharacterComparer(characterStore), gameStatsStore);
        return new Setup(manager, gameStateStore, playerStatsStore, gameStatsStore, dayService, target, incorrectGuess,
            [incorrectGuess, lose4, lose5, lose6, lose7, lose8, lose9]);
    }

    private sealed record Setup(
        GameStateManager Manager,
        InMemoryGameStateStore GameStateStore,
        InMemoryPlayerStatsStore PlayerStatsStore,
        InMemoryGameStatsStore GameStatsStore,
        DayService DayService,
        Character Target,
        Character IncorrectGuess,
        IReadOnlyList<Character> LosingGuesses)
    {
        // Production derives player statistics from persisted sessions, never from incremental counters.
        public PlayerStats ProjectedStats() =>
            PlayerStatsProjection.Create(PlayerStatsStore.Stats, GameStatsStore.Sessions.Values, DayService);
    }
}
