using FluentAssertions;
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
        await setup.Manager.StartGameAsync(GameMode.Daily);

        await setup.Manager.MakeGuessAsync(setup.IncorrectGuess.Id);

        setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId].StartedOnUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task ValidGuessProducesExpectedPersistedStatisticsState()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Daily);

        await setup.Manager.MakeGuessAsync(setup.IncorrectGuess.Id);

        var session = setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId];
        session.Guesses.Should().ContainSingle();
        session.Guesses.First().GuessedCharacterId.Should().Be(setup.IncorrectGuess.Id);
        setup.PlayerStatsStore.Stats.LastPlayedDailyGameId.Should().Be(setup.Manager.GameState.GameId);
    }

    [Fact]
    public async Task WinningCompletionIsRecorded()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Daily);

        await setup.Manager.MakeGuessAsync(setup.Target.Id);

        var session = setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId];
        session.FinishedOnUtc.Should().NotBeNull();
        session.IsWon.Should().BeTrue();
        setup.PlayerStatsStore.Stats.GamesWon.Should().Be(1);
        setup.PlayerStatsStore.Stats.GamesPlayed.Should().Be(1);
    }

    [Fact]
    public async Task LosingCompletionIsRecorded()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Daily);

        foreach (var guess in setup.LosingGuesses)
        {
            await setup.Manager.MakeGuessAsync(guess.Id);
        }

        var session = setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId];
        session.FinishedOnUtc.Should().NotBeNull();
        session.IsWon.Should().BeFalse();
        setup.PlayerStatsStore.Stats.GamesPlayed.Should().Be(1);
        setup.PlayerStatsStore.Stats.GamesWon.Should().Be(0);
    }

    [Fact]
    public async Task UnfinishedGameDoesNotProduceCompletionStatistics()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Daily);

        await setup.Manager.MakeGuessAsync(setup.IncorrectGuess.Id);

        setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId].FinishedOnUtc.Should().BeNull();
        setup.PlayerStatsStore.Stats.GamesPlayed.Should().Be(0);
    }

    [Fact]
    public async Task DuplicateRejectedGuessDoesNotProduceAdditionalGuessStatistics()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Daily);

        await setup.Manager.MakeGuessAsync(setup.IncorrectGuess.Id);
        await setup.Manager.MakeGuessAsync(setup.IncorrectGuess.Id);

        setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId].Guesses.Should().ContainSingle();
    }

    [Fact]
    public async Task CompletionRecordingRemainsIdempotent()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Daily);

        await setup.Manager.MakeGuessAsync(setup.Target.Id);
        await setup.Manager.MarkStatsRecordedAsync();
        await setup.Manager.MarkStatsRecordedAsync();

        setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId].FinishedOnUtc.Should().NotBeNull();
        setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId].IsWon.Should().BeTrue();
        setup.Manager.GameState.StatsRecorded.Should().BeTrue();
    }

    [Fact]
    public async Task DailyPlayerStatsAreUpdatedAccordingToCurrentSemantics()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Daily);

        await setup.Manager.MakeGuessAsync(setup.Target.Id);

        setup.PlayerStatsStore.Stats.GamesPlayed.Should().Be(1);
        setup.PlayerStatsStore.Stats.GamesWon.Should().Be(1);
        setup.PlayerStatsStore.Stats.LastCompletedDayNumber.Should().Be(3);
        setup.PlayerStatsStore.Stats.LastPlayedDailyGameId.Should().Be(setup.Manager.GameState.GameId);
        setup.PlayerStatsStore.Stats.GuessDistribution[0].Should().Be(1);
    }

    [Fact]
    public async Task GameSessionContainsStartFinishWinLossAndGuesses()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Daily);

        await setup.Manager.MakeGuessAsync(setup.Target.Id);

        var session = setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId];
        session.PlayerId.Should().Be(setup.PlayerStatsStore.Stats.Id);
        session.Mode.Should().Be(GameMode.Daily);
        session.DailyNumber.Should().Be(3);
        session.TargetCharacterId.Should().Be(setup.Target.Id);
        session.FinishedOnUtc.Should().NotBeNull();
        session.Guesses.Should().ContainSingle();
        session.Guesses.First().GuessedCharacterId.Should().Be(setup.Target.Id);
    }

    private static Setup CreateSetup()
    {
        var target = CreateCharacter(1, "Target", joinDay: 10, languages: 2, pronouns: ["Any"], affiliations: ["Guild"], species: ["Human"]);
        var incorrectGuess = CreateCharacter(2, "IncorrectGuess", joinDay: 20, languages: 4, pronouns: ["He/Him"], affiliations: ["Other"], species: ["Unknown"]);
        var lose4 = CreateCharacter(4, "Lose4", joinDay: 40, languages: 1, pronouns: ["They/Them"], affiliations: ["Other"], species: ["Human"]);
        var lose5 = CreateCharacter(5, "Lose5", joinDay: 50, languages: 2, pronouns: ["Any"], affiliations: ["Other"], species: ["Human"]);
        var lose6 = CreateCharacter(6, "Lose6", joinDay: 60, languages: 3, pronouns: ["He/Him"], affiliations: ["Other"], species: ["Human"]);
        var lose7 = CreateCharacter(7, "Lose7", joinDay: 70, languages: 4, pronouns: ["She/Her"], affiliations: ["Other"], species: ["Human"]);
        var characterStore = new InMemoryCharacterStore([target, incorrectGuess, lose4, lose5, lose6, lose7], target.Id, new Dictionary<int, int> { [3] = target.Id });
        var playerStatsStore = new InMemoryPlayerStatsStore { Stats = new PlayerStats { Id = Guid.Parse("11111111-1111-1111-1111-111111111111") } };
        var gameStateStore = new InMemoryGameStateStore();
        var gameStatsStore = new InMemoryGameStatsStore();
        var statisticsService = new StatisticsService(playerStatsStore, gameStatsStore);
        var manager = new GameStateManager(gameStateStore, new GameService(characterStore), new InMemoryPlayerStatsStore(), characterStore, new CharacterComparer(characterStore), statisticsService);
        return new Setup(manager, gameStateStore, playerStatsStore, gameStatsStore, statisticsService, target, incorrectGuess, [incorrectGuess, lose4, lose5, lose6, lose7]);
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
        StatisticsService StatisticsService,
        Character Target,
        Character IncorrectGuess,
        IReadOnlyList<Character> LosingGuesses);
}
