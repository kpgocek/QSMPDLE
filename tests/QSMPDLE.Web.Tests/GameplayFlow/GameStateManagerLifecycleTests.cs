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

        var result = await setup.Manager.StartGameAsync(EntryPoint.Practice);

        result.Should().Be(Extensions.LoadGameResult.CreatedNew);
        setup.Manager.GameState.EntryPoint.Should().Be(EntryPoint.Practice);
        setup.Manager.GameState.PlayerId.Should().Be(setup.PlayerId);
        setup.Manager.GameState.Game.TargetId.Should().Be(setup.Target.Id);
        setup.GameStateStore.State.Should().NotBeNull();
        setup.GameStateStore.State!.EntryPoint.Should().Be(EntryPoint.Practice);
    }

    [Fact]
    public async Task ExistingUnfinishedGameIsRestored()
    {
        var setup = CreateSetup();
        var restored = CreateState(Guid.NewGuid(), EntryPoint.Daily, setup.Target.Id, setup.PlayerId);
        restored.Game.PuzzleId = new DayService().GetTodayDayNumber();
        setup.GameStateStore.State = restored;
        setup.PlayerStatsStore.Stats.Id = setup.PlayerId;

        var result = await setup.Manager.StartGameAsync(EntryPoint.Daily);

        result.Should().Be(Extensions.LoadGameResult.LoadedExisting);
        setup.Manager.GameState.GameId.Should().Be(restored.GameId);
        setup.Manager.GameState.PlayerId.Should().Be(setup.PlayerId);
        setup.GameStateStore.State!.PlayerId.Should().Be(setup.PlayerId);
    }

    [Fact]
    public async Task FinishedGameIsRestored()
    {
        var setup = CreateSetup();
        var restored = CreateState(Guid.NewGuid(), EntryPoint.Practice, setup.Target.Id, setup.PlayerId);
        restored.IsWon = true;
        restored.SeenPopup = true;
        restored.GuessesMade.Add(CreateGuess(setup.Target.Id, setup.Target.Name, true));
        setup.GameStateStore.State = restored;

        var result = await setup.Manager.StartGameAsync(EntryPoint.Practice);

        result.Should().Be(Extensions.LoadGameResult.LoadedExisting);
        setup.Manager.GameState.IsFinished.Should().BeTrue();
        setup.Manager.GameState.IsWon.Should().BeTrue();
        setup.Manager.GameState.GuessesMade.Should().HaveCount(1);
    }

    [Fact]
    public async Task PracticeGameStartsWithFreshState()
    {
        var setup = CreateSetup();

        await setup.Manager.StartNewPracticeGameAsync();

        setup.Manager.GameState.EntryPoint.Should().Be(EntryPoint.Practice);
        setup.Manager.GameState.IsWon.Should().BeFalse();
        setup.Manager.GameState.IsLost.Should().BeFalse();
        setup.Manager.GameState.GuessesMade.Should().BeEmpty();
        setup.Manager.GameState.PlayerId.Should().Be(setup.PlayerId);
    }

    [Fact]
    public async Task ArchiveGameLoadsCorrectTargetAndState()
    {
        var setup = CreateSetup(archiveDayNumber: 7);

        var result = await setup.Manager.StartGameAsync(EntryPoint.Archive, 7);

        result.Should().Be(Extensions.LoadGameResult.CreatedNew);
        setup.Manager.GameState.EntryPoint.Should().Be(EntryPoint.Archive);
        setup.Manager.GameState.Game.PuzzleId.Should().Be(7);
        setup.Manager.GameState.Game.TargetId.Should().Be(setup.Target.Id);
    }

    [Fact]
    public async Task InvalidArchiveInitializationReturnsFailed()
    {
        var setup = CreateSetup();

        var result = await setup.Manager.StartGameAsync(EntryPoint.Archive, null);

        result.Should().Be(Extensions.LoadGameResult.Failed);
        setup.GameStateStore.State.Should().BeNull();
    }

    [Fact]
    public async Task DailyProgressIsContinuedByArchiveAsTheSameCanonicalSession()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Daily);
        var puzzleId = setup.Manager.GameState.Game.PuzzleId!.Value;
        var gameId = setup.Manager.GameState.GameId;

        await setup.Manager.MakeGuessAsync(setup.Guess.Id);
        var result = await setup.Manager.StartGameAsync(EntryPoint.Archive, puzzleId);

        result.Should().Be(Extensions.LoadGameResult.LoadedExisting);
        setup.Manager.GameState.GameId.Should().Be(gameId);
        setup.Manager.GameState.Game.PuzzleId.Should().Be(puzzleId);
        setup.Manager.GameState.GuessesMade.Should().ContainSingle();
    }

    [Fact]
    public async Task CompletedCanonicalPuzzleIsReplayOnlyFromArchive()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(EntryPoint.Daily);
        var puzzleId = setup.Manager.GameState.Game.PuzzleId!.Value;
        var gameId = setup.Manager.GameState.GameId;
        await setup.Manager.MakeGuessAsync(setup.Target.Id);

        await setup.Manager.StartGameAsync(EntryPoint.Archive, puzzleId);

        setup.Manager.GameState.GameId.Should().Be(gameId);
        setup.Manager.GameState.IsWon.Should().BeTrue();
        setup.Manager.GameState.IsFinished.Should().BeTrue();
    }

    [Fact]
    public async Task NewPracticeGameAlwaysCreatesAnIndependentSession()
    {
        var setup = CreateSetup();
        await setup.Manager.StartNewPracticeGameAsync();
        var first = setup.Manager.GameState.GameId;

        await setup.Manager.StartNewPracticeGameAsync();

        setup.Manager.GameState.GameId.Should().NotBe(first);
        setup.Manager.GameState.SessionCategory.Should().Be(SessionCategory.Practice);
        setup.Manager.GameState.Game.PuzzleId.Should().BeNull();
    }

    [Fact]
    public async Task FirstCanonicalGuessAdoptsTheSessionClaimedByAnotherClient()
    {
        var setup = CreateSetup(archiveDayNumber: 7);
        await setup.Manager.StartGameAsync(EntryPoint.Archive, 7);
        var otherGameId = Guid.NewGuid();
        setup.GameStatsStore.Sessions[otherGameId] = new GameSession
        {
            GameId = otherGameId,
            PlayerId = setup.PlayerId,
            PuzzleId = 7,
            SessionCategory = SessionCategory.CanonicalPuzzle,
            FirstEntryPoint = EntryPoint.Daily,
            TargetCharacterId = setup.Target.Id,
        };

        var result = await setup.Manager.MakeGuessAsync(setup.Guess.Id);

        result.Should().BeNull();
        setup.Manager.GameState.GameId.Should().Be(otherGameId);
        setup.Manager.GameState.GuessesMade.Should().BeEmpty();
    }

    [Fact]
    public async Task FirstGame_NewPlayer_IsFirstUntilTheNextGame()
    {
        var setup = CreateSetup();

        await setup.Manager.StartGameAsync(EntryPoint.Practice);
        setup.Manager.IsFirstGame.Should().BeTrue();

        await setup.Manager.MakeGuessAsync(setup.Guess.Id);
        setup.GameStateStore.FirstGameId.Should().Be(setup.Manager.GameState.GameId);

        await setup.Manager.StartGameAsync(EntryPoint.Practice);
        setup.Manager.IsFirstGame.Should().BeTrue("reloading the first game keeps its legend");

        await setup.Manager.StartNewPracticeGameAsync();
        setup.Manager.IsFirstGame.Should().BeFalse();
    }

    [Fact]
    public async Task FirstGame_PlayerWithEarlierGuesses_IsNeverFirstAndIsRememberedAsVeteran()
    {
        var setup = CreateSetup(archiveDayNumber: 7);
        var earlier = CreateState(Guid.NewGuid(), EntryPoint.Archive, setup.Target.Id, setup.PlayerId);
        earlier.GuessesMade.Add(CreateGuess(setup.Guess.Id, setup.Guess.Name, false));
        setup.GameStateStore.SetStateForKey("canonical-7", earlier);

        await setup.Manager.StartGameAsync(EntryPoint.Practice);
        await setup.Manager.MakeGuessAsync(setup.Guess.Id);

        setup.Manager.IsFirstGame.Should().BeFalse();
        setup.GameStateStore.FirstGameId.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task FirstGame_UnguessedGameElsewhere_DoesNotCountAsPlayed()
    {
        var setup = CreateSetup();
        setup.GameStateStore.SetStateForKey("canonical-7", CreateState(Guid.NewGuid(), EntryPoint.Archive, setup.Target.Id, setup.PlayerId));

        await setup.Manager.StartGameAsync(EntryPoint.Practice);

        setup.Manager.IsFirstGame.Should().BeTrue();
        setup.GameStateStore.FirstGameId.Should().BeNull();
    }

    private static Setup CreateSetup(int? archiveDayNumber = null)
    {
        var target = TestCharacters.Create(1, "Target", joinDay: 10, languages: 2, pronouns: ["Any"], affiliations: ["Guild"], species: ["Human"]);
        var guess = TestCharacters.Create(2, "Guess", joinDay: 20, languages: 4, pronouns: ["He/Him"], affiliations: ["Other"], species: ["Unknown"]);
        var characters = new[] { target, guess };
        var characterStore = new InMemoryCharacterStore(characters, target.Id, archiveDayNumber is null ? null : new Dictionary<int, int> { [archiveDayNumber.Value] = target.Id });
        var playerStatsStore = new InMemoryPlayerStatsStore { Stats = new PlayerStats { Id = Guid.Parse("11111111-1111-1111-1111-111111111111") } };
        var gameStateStore = new InMemoryGameStateStore();
        var gameStatsStore = new InMemoryGameStatsStore();
        var playerId = playerStatsStore.Stats.Id;
        var dayService = new DayService();
        var manager = new GameStateManager(gameStateStore, new GameService(characterStore, dayService), dayService, playerStatsStore, characterStore, new CharacterComparer(characterStore), gameStatsStore);
        return new Setup(manager, gameStateStore, playerStatsStore, gameStatsStore, characterStore, target, guess, playerId);
    }

    private static GameState CreateState(Guid gameId, EntryPoint entryPoint, int targetId, Guid playerId) => new()
    {
        GameId = gameId,
        Game = new Game { TargetId = targetId, PortraitUrl = "https://example.com/portrait.png", PuzzleId = entryPoint == EntryPoint.Practice ? null : 1 },
        PlayerId = playerId,
    };

    private static GuessResult CreateGuess(int characterId, string name, bool isCorrect) => new()
    {
        Character = new CharacterLookup(characterId, name, name.ToLowerInvariant(), [], $"https://example.com/{characterId}.png"),
        IsCorrect = isCorrect,
        Pronouns = ComparisonResult.Correct,
        Languages = ComparisonResult.Correct,
        Joined = ComparisonResult.Correct,
        Affiliation = ComparisonResult.Correct,
        Species = ComparisonResult.Correct,
    };

    private sealed record Setup(GameStateManager Manager, InMemoryGameStateStore GameStateStore, InMemoryPlayerStatsStore PlayerStatsStore, InMemoryGameStatsStore GameStatsStore, InMemoryCharacterStore CharacterStore, Character Target, Character Guess, Guid PlayerId);
}
