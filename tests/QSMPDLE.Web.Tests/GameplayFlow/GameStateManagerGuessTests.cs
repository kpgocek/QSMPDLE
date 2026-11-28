using FluentAssertions;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Tests.GameplayFlow;

public sealed class GameStateManagerGuessTests
{
    [Fact]
    public async Task ValidGuessIsAccepted()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Practice);

        var result = await setup.Manager.MakeGuessAsync(setup.GuessOne.Id);

        result.Should().NotBeNull();
        setup.Manager.GameState.GuessesMade.Should().ContainSingle();
        setup.GameStateStore.State!.GuessesMade.Should().ContainSingle();
    }

    [Fact]
    public async Task DuplicateGuessIsRejected()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Practice);
        await setup.Manager.MakeGuessAsync(setup.GuessOne.Id);

        var result = await setup.Manager.MakeGuessAsync(setup.GuessOne.Id);

        result.Should().BeNull();
        setup.Manager.GameState.GuessesMade.Should().HaveCount(1);
    }

    [Fact]
    public async Task GuessAfterGameCompletionIsRejected()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Practice);
        await setup.Manager.MakeGuessAsync(setup.CorrectGuess.Id);

        var result = await setup.Manager.MakeGuessAsync(setup.GuessOne.Id);

        result.Should().BeNull();
        setup.Manager.GameState.GuessesMade.Should().ContainSingle();
    }

    [Fact]
    public async Task IncorrectGuessUpdatesGameStateCorrectly()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Practice);

        var result = await setup.Manager.MakeGuessAsync(setup.GuessOne.Id);

        result.Should().NotBeNull();
        setup.Manager.GameState.IsWon.Should().BeFalse();
        setup.Manager.GameState.IsLost.Should().BeFalse();
        setup.Manager.GameState.IsFinished.Should().BeFalse();
        setup.Manager.GameState.GuessesMade.Should().ContainSingle();
        setup.Manager.GameState.GuessesMade[0].IsCorrect.Should().BeFalse();
    }

    [Fact]
    public async Task CorrectGuessWinsGame()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Practice);

        await setup.Manager.MakeGuessAsync(setup.CorrectGuess.Id);

        setup.Manager.GameState.IsWon.Should().BeTrue();
        setup.Manager.GameState.IsFinished.Should().BeTrue();
        setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId].IsWon.Should().BeTrue();
    }

    [Fact]
    public async Task FinalIncorrectGuessLosesGame()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Practice);

        foreach (var guess in setup.LosingGuesses)
        {
            await setup.Manager.MakeGuessAsync(guess.Id);
        }

        setup.Manager.GameState.IsLost.Should().BeTrue();
        setup.Manager.GameState.IsFinished.Should().BeTrue();
        setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId].FinishedOnUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ComparisonResultsAreCorrect()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Practice);

        var result = await setup.Manager.MakeGuessAsync(setup.ScenarioGuess.Id);

        result.Should().NotBeNull();
        result!.Joined.Should().Be(ComparisonResult.Earlier);
        result.Languages.Should().Be(ComparisonResult.Less);
        result.Pronouns.Should().Be(ComparisonResult.Partial);
        result.Affiliation.Should().Be(ComparisonResult.Correct);
        result.Species.Should().Be(ComparisonResult.Wrong);
        result.Character.Id.Should().Be(setup.ScenarioGuess.Id);
    }

    [Fact]
    public async Task AcceptedGuessesArePersisted()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Practice);

        await setup.Manager.MakeGuessAsync(setup.GuessOne.Id);

        setup.GameStateStore.State!.GuessesMade.Should()
            .ContainSingle(guess => guess.Character.Id == setup.GuessOne.Id);
    }

    [Fact]
    public async Task GuessCountAndOrderArePreserved()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Practice);

        await setup.Manager.MakeGuessAsync(setup.GuessOne.Id);
        await setup.Manager.MakeGuessAsync(setup.GuessTwo.Id);
        await setup.Manager.MakeGuessAsync(setup.GuessThree.Id);

        setup.GameStateStore.State!.GuessesMade.Select(guess => guess.Character.Id).Should()
            .Equal(setup.ExpectedGuessOrder);
    }

    [Fact]
    public async Task CompetingCanonicalSessionReplacesStateWithoutAddingGuess()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Daily);
        var state = setup.Manager.GameState;
        var existing = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = state.PlayerId,
            SessionCategory = SessionCategory.CanonicalPuzzle,
            PuzzleId = state.Game.PuzzleId,
            TargetCharacterId = setup.CorrectGuess.Id,
            IsWon = true,
            FinishedOnUtc = DateTimeOffset.UtcNow,
            Guesses = [new GameGuess { GuessedCharacterId = setup.CorrectGuess.Id, GuessOrder = 1 }]
        };
        setup.GameStatsStore.Sessions[existing.GameId] = existing;
        Assert.Null(await setup.Manager.MakeGuessAsync(setup.GuessOne.Id));
        Assert.Equal(existing.GameId, setup.Manager.GameState.GameId);
        Assert.True(setup.Manager.GameState.IsWon);
        Assert.Single(setup.GameStateStore.State!.GuessesMade);
    }

    private static Setup CreateSetup()
    {
        var target = TestCharacters.Create(1, "Target", 10, 2, ["Any"], ["Guild"], ["Human"]);
        var guessOne = TestCharacters.Create(2, "GuessOne", 20, 4, ["He/Him"], ["Other"], ["Unknown"]);
        var guessTwo = TestCharacters.Create(3, "GuessTwo", 30, 3, ["She/Her"], ["Third"], ["Non-Human"]);
        var guessThree = TestCharacters.Create(4, "GuessThree", 40, 1, ["They/Them"], ["Fourth"], ["Human"]);
        var guessFour = TestCharacters.Create(5, "GuessFour", 50, 2, ["Any"], ["Fifth"], ["Human"]);
        var guessFive = TestCharacters.Create(6, "GuessFive", 60, 3, ["He/Him"], ["Sixth"], ["Human"]);
        var guessSix = TestCharacters.Create(7, "GuessSix", 70, 4, ["She/Her"], ["Seventh"], ["Human"]);
        var scenarioGuess = TestCharacters.Create(8, "ScenarioGuess", 20, 4, ["He/Him"], ["Guild"], ["Unknown"]);
        var characters = new[]
        {
            target, guessOne, guessTwo, guessThree, guessFour, guessFive, guessSix, scenarioGuess
        };
        var characterStore = new InMemoryCharacterStore(characters, target.Id);
        var playerStatsStore = new InMemoryPlayerStatsStore
        {
            Stats = new PlayerStats { Id = Guid.Parse("11111111-1111-1111-1111-111111111111") }
        };
        var gameStateStore = new InMemoryGameStateStore();
        var gameStatsStore = new InMemoryGameStatsStore();
        var dayService = new DayService();
        var manager = new GameStateManager(gameStateStore, new GameService(characterStore, dayService), dayService,
            new InMemoryPlayerStatsStore(), characterStore, new CharacterComparer(characterStore), gameStatsStore);

        return new Setup(manager, gameStateStore, playerStatsStore, gameStatsStore, target, guessOne, guessTwo,
            guessThree, guessFour, guessFive, guessSix, scenarioGuess, [guessOne.Id, guessTwo.Id, guessThree.Id]);
    }

    private sealed record Setup(
        GameStateManager Manager,
        InMemoryGameStateStore GameStateStore,
        InMemoryPlayerStatsStore PlayerStatsStore,
        InMemoryGameStatsStore GameStatsStore,
        Character Target,
        Character GuessOne,
        Character GuessTwo,
        Character GuessThree,
        Character GuessFour,
        Character GuessFive,
        Character GuessSix,
        Character ScenarioGuess,
        IReadOnlyList<int> ExpectedGuessOrder)
    {
        public IReadOnlyList<Character> LosingGuesses =>
            [GuessOne, GuessTwo, GuessThree, GuessFour, GuessFive, GuessSix];

        public Character CorrectGuess => Target;
    }
}
