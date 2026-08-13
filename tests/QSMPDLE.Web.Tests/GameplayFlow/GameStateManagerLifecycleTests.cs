using FluentAssertions;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Tests.GameplayFlow;

public sealed class GameStateManagerLifecycleTests
{
    [Fact]
    public async Task NewGameStartsSuccessfully()
    {
        var setup = CreateSetup();

        var result = await setup.Manager.StartGameAsync(GameMode.Practice);

        result.Should().Be(Extensions.LoadGameResult.CreatedNew);
        setup.Manager.GameState.GameMode.Should().Be(GameMode.Practice);
        setup.Manager.GameState.PlayerId.Should().Be(setup.PlayerId);
        setup.Manager.GameState.Game.TargetId.Should().Be(setup.Target.Id);
        setup.GameStateStore.State.Should().NotBeNull();
        setup.GameStateStore.State!.GameMode.Should().Be(GameMode.Practice);
    }

    [Fact]
    public async Task ExistingUnfinishedGameIsRestored()
    {
        var setup = CreateSetup();
        var restored = CreateState(Guid.NewGuid(), GameMode.Daily, setup.Target.Id, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        setup.GameStateStore.State = restored;
        setup.PlayerStatsStore.Stats.Id = setup.PlayerId;

        var result = await setup.Manager.StartGameAsync(GameMode.Daily);

        result.Should().Be(Extensions.LoadGameResult.LoadedExisting);
        setup.Manager.GameState.GameId.Should().Be(restored.GameId);
        setup.Manager.GameState.PlayerId.Should().Be(setup.PlayerId);
        setup.GameStateStore.State!.PlayerId.Should().Be(setup.PlayerId);
    }

    [Fact]
    public async Task FinishedGameIsRestored()
    {
        var setup = CreateSetup();
        var restored = CreateState(Guid.NewGuid(), GameMode.Practice, setup.Target.Id, setup.PlayerId);
        restored.IsWon = true;
        restored.StatsRecorded = true;
        restored.SeenPopup = true;
        restored.GuessesMade.Add(CreateGuess(setup.Target.Id, setup.Target.Name, true, false));
        setup.GameStateStore.State = restored;

        var result = await setup.Manager.StartGameAsync(GameMode.Practice);

        result.Should().Be(Extensions.LoadGameResult.LoadedExisting);
        setup.Manager.GameState.IsFinished.Should().BeTrue();
        setup.Manager.GameState.IsWon.Should().BeTrue();
        setup.Manager.GameState.StatsRecorded.Should().BeTrue();
        setup.Manager.GameState.GuessesMade.Should().HaveCount(1);
    }

    [Fact]
    public async Task PracticeGameStartsWithFreshState()
    {
        var setup = CreateSetup();

        await setup.Manager.StartNewPracticeGameAsync();

        setup.Manager.GameState.GameMode.Should().Be(GameMode.Practice);
        setup.Manager.GameState.IsWon.Should().BeFalse();
        setup.Manager.GameState.IsLost.Should().BeFalse();
        setup.Manager.GameState.GuessesMade.Should().BeEmpty();
        setup.Manager.GameState.PlayerId.Should().Be(setup.PlayerId);
    }

    [Fact]
    public async Task ArchiveGameLoadsCorrectTargetAndState()
    {
        var setup = CreateSetup(archiveDayNumber: 7);

        var result = await setup.Manager.StartGameAsync(GameMode.Archive, 7);

        result.Should().Be(Extensions.LoadGameResult.CreatedNew);
        setup.Manager.GameState.GameMode.Should().Be(GameMode.Archive);
        setup.Manager.GameState.Game.DayNumber.Should().Be(7);
        setup.Manager.GameState.Game.TargetId.Should().Be(setup.Target.Id);
    }

    [Fact]
    public async Task InvalidArchiveInitializationReturnsFailed()
    {
        var setup = CreateSetup();

        var result = await setup.Manager.StartGameAsync(GameMode.Archive, null);

        result.Should().Be(Extensions.LoadGameResult.Failed);
        setup.GameStateStore.State.Should().BeNull();
    }

    private static Setup CreateSetup(int? archiveDayNumber = null)
    {
        var target = CreateCharacter(1, "Target", joinDay: 10, languages: 2, pronouns: ["Any"], affiliations: ["Guild"], species: ["Human"]);
        var guess = CreateCharacter(2, "Guess", joinDay: 20, languages: 4, pronouns: ["He/Him"], affiliations: ["Other"], species: ["Unknown"]);
        var characters = new[] { target, guess };
        var characterStore = new InMemoryCharacterStore(characters, target.Id, archiveDayNumber is null ? null : new Dictionary<int, int> { [archiveDayNumber.Value] = target.Id });
        var playerStatsStore = new InMemoryPlayerStatsStore { Stats = new PlayerStats { Id = Guid.Parse("11111111-1111-1111-1111-111111111111") } };
        var gameStateStore = new InMemoryGameStateStore();
        var gameStatsStore = new InMemoryGameStatsStore();
        var statisticsService = new InMemoryStatisticsService();
        var playerId = playerStatsStore.Stats.Id;
        var manager = new GameStateManager(gameStateStore, new GameService(characterStore), new InMemoryPlayerStatsStore(), characterStore, new CharacterComparer(characterStore), statisticsService);
        return new Setup(manager, gameStateStore, playerStatsStore, gameStatsStore, characterStore, target, guess, playerId);
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

    private static GameState CreateState(Guid gameId, GameMode mode, int targetId, Guid playerId) => new()
    {
        GameId = gameId,
        Game = new Game { TargetId = targetId, PortraitUrl = "https://example.com/portrait.png", DayNumber = 1 },
        GameMode = mode,
        PlayerId = playerId,
    };

    private static GuessResult CreateGuess(int characterId, string name, bool isCorrect, bool isLastAllowedGuess) => new()
    {
        Character = new CharacterLookup(characterId, name, name.ToLowerInvariant(), [], $"https://example.com/{characterId}.png"),
        IsCorrect = isCorrect,
        IsLastAllowedGuess = isLastAllowedGuess,
        IsFirstGuess = true,
        Pronouns = ComparisonResult.Correct,
        Languages = ComparisonResult.Correct,
        Joined = ComparisonResult.Correct,
        Affiliation = ComparisonResult.Correct,
        Species = ComparisonResult.Correct,
    };

    private sealed record Setup(GameStateManager Manager, InMemoryGameStateStore GameStateStore, InMemoryPlayerStatsStore PlayerStatsStore, InMemoryGameStatsStore GameStatsStore, InMemoryCharacterStore CharacterStore, Character Target, Character Guess, Guid PlayerId);
}
