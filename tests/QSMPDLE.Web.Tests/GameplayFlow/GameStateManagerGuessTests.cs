using FluentAssertions;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Features.Statistics.Services;

namespace QSMPDLE.Web.Tests.GameplayFlow;

public sealed class GameStateManagerGuessTests
{
    [Fact]
    public async Task ValidGuessIsAccepted()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Practice);

        var result = await setup.Manager.MakeGuessAsync(setup.GuessOne.Id);

        result.Should().NotBeNull();
        setup.Manager.GameState.GuessesMade.Should().ContainSingle();
        setup.GameStateStore.State!.GuessesMade.Should().ContainSingle();
    }

    [Fact]
    public async Task DuplicateGuessIsRejected()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Practice);
        await setup.Manager.MakeGuessAsync(setup.GuessOne.Id);

        var result = await setup.Manager.MakeGuessAsync(setup.GuessOne.Id);

        result.Should().BeNull();
        setup.Manager.GameState.GuessesMade.Should().HaveCount(1);
    }

    [Fact]
    public async Task GuessAfterGameCompletionIsRejected()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Practice);
        await setup.Manager.MakeGuessAsync(setup.CorrectGuess.Id);

        var result = await setup.Manager.MakeGuessAsync(setup.GuessOne.Id);

        result.Should().BeNull();
        setup.Manager.GameState.GuessesMade.Should().ContainSingle();
    }

    [Fact]
    public async Task IncorrectGuessUpdatesGameStateCorrectly()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Practice);

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
        await setup.Manager.StartGameAsync(GameMode.Practice);

        await setup.Manager.MakeGuessAsync(setup.CorrectGuess.Id);

        setup.Manager.GameState.IsWon.Should().BeTrue();
        setup.Manager.GameState.IsFinished.Should().BeTrue();
        setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId].IsWon.Should().BeTrue();
    }

    [Fact]
    public async Task FinalIncorrectGuessLosesGame()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Practice);

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
        await setup.Manager.StartGameAsync(GameMode.Practice);

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
        await setup.Manager.StartGameAsync(GameMode.Practice);

        await setup.Manager.MakeGuessAsync(setup.GuessOne.Id);

        setup.GameStateStore.State!.GuessesMade.Should().ContainSingle(guess => guess.Character.Id == setup.GuessOne.Id);
    }

    [Fact]
    public async Task GuessCountAndOrderArePreserved()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Practice);

        await setup.Manager.MakeGuessAsync(setup.GuessOne.Id);
        await setup.Manager.MakeGuessAsync(setup.GuessTwo.Id);
        await setup.Manager.MakeGuessAsync(setup.GuessThree.Id);

        setup.GameStateStore.State!.GuessesMade.Select(guess => guess.Character.Id).Should().Equal(setup.ExpectedGuessOrder);
    }

    private static Setup CreateSetup()
    {
        var target = CreateCharacter(1, "Target", joinDay: 10, languages: 2, pronouns: ["Any"], affiliations: ["Guild"], species: ["Human"]);
        var guessOne = CreateCharacter(2, "GuessOne", joinDay: 20, languages: 4, pronouns: ["He/Him"], affiliations: ["Other"], species: ["Unknown"]);
        var guessTwo = CreateCharacter(3, "GuessTwo", joinDay: 30, languages: 3, pronouns: ["She/Her"], affiliations: ["Third"], species: ["Non-Human"]);
        var guessThree = CreateCharacter(4, "GuessThree", joinDay: 40, languages: 1, pronouns: ["They/Them"], affiliations: ["Fourth"], species: ["Human"]);
        var guessFour = CreateCharacter(5, "GuessFour", joinDay: 50, languages: 2, pronouns: ["Any"], affiliations: ["Fifth"], species: ["Human"]);
        var guessFive = CreateCharacter(6, "GuessFive", joinDay: 60, languages: 3, pronouns: ["He/Him"], affiliations: ["Sixth"], species: ["Human"]);
        var guessSix = CreateCharacter(7, "GuessSix", joinDay: 70, languages: 4, pronouns: ["She/Her"], affiliations: ["Seventh"], species: ["Human"]);
        var scenarioGuess = CreateCharacter(8, "ScenarioGuess", joinDay: 20, languages: 4, pronouns: ["He/Him"], affiliations: ["Guild"], species: ["Unknown"]);
        var characters = new[] { target, guessOne, guessTwo, guessThree, guessFour, guessFive, guessSix, scenarioGuess };
        var characterStore = new InMemoryCharacterStore(characters, target.Id);
        var playerStatsStore = new InMemoryPlayerStatsStore { Stats = new PlayerStats { Id = Guid.Parse("11111111-1111-1111-1111-111111111111") } };
        var gameStateStore = new InMemoryGameStateStore();
        var gameStatsStore = new InMemoryGameStatsStore();
        var statisticsService = new StatisticsService(playerStatsStore, gameStatsStore);
        var manager = new GameStateManager(gameStateStore, new GameService(characterStore), new InMemoryPlayerStatsStore(), characterStore, new CharacterComparer(characterStore), statisticsService);

        return new Setup(manager, gameStateStore, playerStatsStore, gameStatsStore, target, guessOne, guessTwo, guessThree, guessFour, guessFive, guessSix, scenarioGuess, [guessOne.Id, guessTwo.Id, guessThree.Id]);
    }

    private static Character CreateCharacter(int id, string name, int joinDay, int languages, string[] pronouns, string[] affiliations, string[] species) => new()
    {
        Id = id,
        Name = name,
        MinecraftUsername = name.ToLowerInvariant(),
        Aliases = [],
        IconUrl = $"https://example.com/{id}.png",
        Pronouns = pronouns.ToList(),
        Languages = languages,
        Affiliations = affiliations.ToList(),
        Species = species.ToList(),
        JoinDayNumber = joinDay,
    };

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
        public IReadOnlyList<Character> LosingGuesses => [GuessOne, GuessTwo, GuessThree, GuessFour, GuessFive, GuessSix];
        public Character CorrectGuess => Target;
    }
}
