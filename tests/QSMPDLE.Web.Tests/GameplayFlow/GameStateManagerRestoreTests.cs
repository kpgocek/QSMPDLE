using FluentAssertions;
using Microsoft.Extensions.Options;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Tests.GameplayFlow;

/// <summary>Saved games that no longer match today's data or identity.</summary>
public sealed class GameStateManagerRestoreTests
{
    private const int PuzzleId = 7;
    private const int AnswerId = 2;
    private const int GuessId = 4;
    private const int MissingId = 99;

    private static readonly Guid PlayerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly InMemoryGameStateStore gameStateStore = new();
    private readonly InMemoryGameStatsStore gameStatsStore = new();
    private readonly GameStateManager manager;
    private readonly InMemoryPlayerStatsStore playerStatsStore = new() { Stats = new PlayerStats { Id = PlayerId } };

    public GameStateManagerRestoreTests()
    {
        manager = CreateManager([.. Enumerable.Range(1, 5).Select(id => CreateCharacter(id))]);
    }

    [Fact]
    public async Task StoredSession_GuessOfARemovedCharacter_LoadsTheOtherGuesses()
    {
        AddSession(AnswerId, MissingId, GuessId);

        await manager.StartGameAsync(EntryPoint.Archive, PuzzleId);

        manager.GameState.GuessesMade.Should().ContainSingle().Which.Character.Id.Should().Be(GuessId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(MissingId)]
    public async Task StoredSession_UnknownAnswer_LoadsItsResult(int target)
    {
        AddSession(target, GuessId).FinishedOnUtc = DateTimeOffset.UtcNow;

        await manager.StartGameAsync(EntryPoint.Archive, PuzzleId);

        manager.GameState.IsLost.Should().BeTrue();
        manager.GameState.GuessesMade.Should().BeEmpty("nothing can be compared with an unknown answer");
    }

    [Fact]
    public async Task SavedGame_OfAnotherPlayerId_IsRecordedAsAGameOfItsOwn()
    {
        var saved = Save(AnswerId, Guid.NewGuid());
        saved.GuessesMade.Add(new GuessResult
        {
            Character = new CharacterLookup(GuessId, "Character 4", null, [], "")
        });

        await manager.StartGameAsync(EntryPoint.Archive, PuzzleId);
        await manager.MakeGuessAsync(3);

        manager.GameState.GameId.Should().NotBe(saved.GameId, "that id is already used under the other player");
        var session = gameStatsStore.Sessions.Values.Should().ContainSingle().Subject;
        session.PlayerId.Should().Be(PlayerId);
        session.GuessedCharacterIds.Should().Equal(GuessId, 3);
    }

    [Fact]
    public async Task SavedGame_WithAnotherAnswer_IsNotPlayed()
    {
        var saved = Save(GuessId, PlayerId);

        await manager.StartGameAsync(EntryPoint.Archive, PuzzleId);

        manager.GameState.Game.TargetId.Should().Be(AnswerId, "only the server decides a puzzle's answer");
        manager.GameState.GameId.Should().NotBe(saved.GameId);
    }

    [Fact]
    public async Task Guess_OfTheAnswer_IsAcceptedWhenItsReleaseDateMovedPastThePuzzle()
    {
        var late = CreateCharacter(AnswerId, new DateOnly(2026, 12, 1));
        var game = CreateManager([CreateCharacter(1), late, CreateCharacter(GuessId)]);
        await game.StartGameAsync(EntryPoint.Archive, PuzzleId);

        (await game.MakeGuessAsync(AnswerId)).Should().NotBeNull();

        game.GameState.IsWon.Should().BeTrue();
    }

    [Fact]
    public async Task NewPracticeGame_WithoutAnIdentity_KeepsTheCurrentGame()
    {
        await manager.StartGameAsync(EntryPoint.Practice);
        var current = manager.GameState.GameId;
        playerStatsStore.Stats.Id = Guid.Empty;

        await FluentActions.Awaiting(() => manager.StartNewPracticeGameAsync()).Should()
            .ThrowAsync<InvalidOperationException>();

        manager.GameState.GameId.Should().Be(current);
    }

    private GameSession AddSession(int target, params int[] guessedCharacterIds)
    {
        var session = new GameSession
        {
            GameId = Guid.NewGuid(), PlayerId = PlayerId, PuzzleId = PuzzleId, TargetCharacterId = target
        };
        foreach (var characterId in guessedCharacterIds)
        {
            session.AddGuess(characterId);
        }

        return gameStatsStore.Sessions[session.GameId] = session;
    }

    private GameState Save(int target, Guid playerId) => gameStateStore.State = new GameState
    {
        GameId = Guid.NewGuid(),
        PlayerId = playerId,
        Game = new Game { PuzzleId = PuzzleId, TargetId = target, PortraitUrl = "" }
    };

    private GameStateManager CreateManager(Character[] characters)
    {
        var characterStore =
            new InMemoryCharacterStore(characters, 1, new Dictionary<int, int> { [PuzzleId] = AnswerId });
        var dayService = new DayService(new TestClock(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero)));
        return new GameStateManager(
            gameStateStore,
            new GameService(characterStore, dayService),
            dayService,
            playerStatsStore,
            characterStore,
            new CharacterComparer(characterStore),
            gameStatsStore,
            availability: new GameplayAvailabilityService(dayService, Options.Create(new GameplayOptions())));
    }

    private static Character CreateCharacter(int id, DateOnly? availableFrom = null) => new()
    {
        Id = id,
        Name = $"Character {id}",
        PortraitId = Guid.NewGuid(),
        Pronouns = ["He/Him"],
        LanguageCount = 1,
        Species = ["Human"],
        JoinDayNumber = id,
        AvailableFrom = availableFrom
    };
}
