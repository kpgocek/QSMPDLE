using FluentAssertions;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Tests.GameplayFlow;

/// <summary>A circuit can start an operation while another one still waits for the database.</summary>
public sealed class GameStateManagerOverlapTests
{
    private readonly TaskCompletionSource database = new();
    private readonly InMemoryGameStateStore gameStateStore = new();
    private readonly InMemoryGameStatsStore gameStatsStore = new();
    private readonly GameStateManager manager;

    public GameStateManagerOverlapTests()
    {
        var characters = Enumerable.Range(1, 5)
            .Select(id => TestCharacters.Create(id, $"Character {id}", id, 1, ["He/Him"], [], ["Human"]));
        var characterStore = new InMemoryCharacterStore(characters, 1, new Dictionary<int, int> { [1] = 2, [2] = 3 });
        var dayService = new DayService();
        manager = new GameStateManager(
            gameStateStore,
            new GameService(characterStore, dayService),
            dayService,
            new InMemoryPlayerStatsStore { Stats = new PlayerStats { Id = Guid.NewGuid() } },
            characterStore,
            new CharacterComparer(characterStore),
            gameStatsStore);
    }

    [Fact]
    public async Task NewPracticeGame_DuringAGuess_KeepsTheNewGame()
    {
        await manager.StartGameAsync(EntryPoint.Practice);
        var previousGameId = manager.GameState.GameId;
        gameStatsStore.Recording = database.Task;

        await ReleaseAndWaitAsync(manager.MakeGuessAsync(4), manager.StartNewPracticeGameAsync());

        manager.GameState.GameId.Should().NotBe(previousGameId);
        manager.GameState.GuessesMade.Should().BeEmpty();
    }

    [Fact]
    public async Task NewPracticeGame_DuringAGuessThatIsNotRecorded_StartsEmpty()
    {
        await manager.StartGameAsync(EntryPoint.Practice);
        gameStatsStore.Recording = database.Task;
        gameStatsStore.FailuresLeft = 1;

        await ReleaseAndWaitAsync(manager.MakeGuessAsync(4), manager.StartNewPracticeGameAsync());

        manager.GameState.GuessesMade.Should().BeEmpty();
    }

    [Fact]
    public async Task ArchiveDay_OpenedDuringAGuess_IsTheGameShownAndSaved()
    {
        await manager.StartGameAsync(EntryPoint.Archive, 1);
        gameStatsStore.Recording = database.Task;

        await ReleaseAndWaitAsync(manager.MakeGuessAsync(4), manager.StartGameAsync(EntryPoint.Archive, 2));

        manager.GameState.Game.PuzzleId.Should().Be(2);
        gameStateStore.GetStateForKey("canonical-1").GuessesMade.Should().ContainSingle();
        gameStateStore.GetStateForKey("canonical-2").Game.PuzzleId.Should().Be(2);
    }

    [Fact]
    public async Task Practice_OpenedDuringASlowLoad_KeepsItsOwnSavedGame()
    {
        gameStatsStore.Loading = database.Task;

        await ReleaseAndWaitAsync(manager.StartGameAsync(EntryPoint.Archive, 1),
            manager.StartGameAsync(EntryPoint.Practice));

        manager.GameState.SessionCategory.Should().Be(SessionCategory.Practice);
        gameStateStore.GetStateForKey("practice-current").Game.PuzzleId.Should().BeNull();
        gameStateStore.GetStateForKey("canonical-1").Game.PuzzleId.Should().Be(1);
    }

    [Fact]
    public async Task Guess_MadeWhileAnotherGameLoads_IsNotPlayedInThatGame()
    {
        await manager.StartGameAsync(EntryPoint.Archive, 1);
        gameStatsStore.Loading = database.Task;

        var load = manager.StartGameAsync(EntryPoint.Archive, 2);
        var guess = manager.MakeGuessAsync(4);
        await ReleaseAndWaitAsync(load, guess);

        (await guess).Should().BeNull();
        manager.GameState.Game.PuzzleId.Should().Be(2);
        manager.GameState.GuessesMade.Should().BeEmpty();
        gameStatsStore.Sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task Popup_ClosedWhileAnotherGameLoads_StaysUnseenInThatGame()
    {
        await manager.StartGameAsync(EntryPoint.Archive, 1);
        gameStatsStore.Loading = database.Task;

        await ReleaseAndWaitAsync(manager.StartGameAsync(EntryPoint.Archive, 2), manager.MarkPopupAsSeenAsync());

        manager.GameState.Game.PuzzleId.Should().Be(2);
        manager.GameState.SeenPopup.Should().BeFalse();
    }

    private async Task ReleaseAndWaitAsync(params Task[] operations)
    {
        database.SetResult();
        await Task.WhenAll(operations);
    }
}
