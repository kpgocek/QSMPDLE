using FluentAssertions;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Tests.GameplayFlow;

public sealed class GameStateManagerArchiveReplayTests
{
    private static readonly string[] AnyPronouns = ["Any"];
    private static readonly string[] GuildAffiliations = ["Guild"];
    private static readonly string[] HumanSpecies = ["Human"];
    private static readonly string[] HeHimPronouns = ["He/Him"];
    private static readonly string[] OtherAffiliations = ["Other"];
    private static readonly string[] UnknownSpecies = ["Unknown"];

    [Fact]
    public async Task ArchiveReplay_UsesCharacterComparerResults()
    {
        // Arrange - create store/service/fakes like existing tests
        var target = TestCharacters.Create(1, "Target", joinDay: 10, languages: 2, pronouns: AnyPronouns, affiliations: GuildAffiliations, species: HumanSpecies);
        var guessOne = TestCharacters.Create(2, "GuessOne", joinDay: 20, languages: 4, pronouns: HeHimPronouns, affiliations: OtherAffiliations, species: UnknownSpecies);
        var characters = new[] { target, guessOne };
        var characterStore = new InMemoryCharacterStore(characters, target.Id);

        var playerStatsStore = new InMemoryPlayerStatsStore { Stats = new PlayerStats { Id = Guid.NewGuid() } };
        var gameStateStore = new InMemoryGameStateStore();
        var gameStatsStore = new InMemoryGameStatsStore();
        var dayService = new DayService();
        var comparer = new CharacterComparer(characterStore);

        var manager = new GameStateManager(gameStateStore, new GameService(characterStore, dayService), dayService, playerStatsStore, characterStore, comparer, gameStatsStore);

        // Create a completed daily session in the stats store
        var completed = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = playerStatsStore.Stats.Id,
            PuzzleId = 5,
            TargetCharacterId = target.Id,
            StartedOnUtc = DateTimeOffset.UtcNow.AddMinutes(-15),
            FinishedOnUtc = DateTimeOffset.UtcNow,
            IsWon = false,
            Guesses = new List<GameGuess>
            {
                new() { GameId = Guid.Empty, GuessOrder = 0, GuessedCharacterId = guessOne.Id }
            }
        };

        gameStatsStore.Sessions[completed.GameId] = completed;

        // Act - start archive which should replay the completed session
        var result = await manager.StartGameAsync(EntryPoint.Archive, 5);

        // Assert
        result.Should().Be(Extensions.LoadGameResult.LoadedExisting);
        manager.GameState.Should().NotBeNull();
        manager.GameState.GuessesMade.Should().HaveCount(1);

        var replayed = manager.GameState.GuessesMade[0];

        var expected = await comparer.CompareAsync(target.Id, guessOne.Id, manager.ActiveClues);

        // Compare fields produced by comparer
        replayed.Character.Id.Should().Be(expected.Character.Id);
        replayed.IsCorrect.Should().Be(expected.IsCorrect);
        replayed.Pronouns.Should().Be(expected.Pronouns);
        replayed.Languages.Should().Be(expected.Languages);
        replayed.Joined.Should().Be(expected.Joined);
        replayed.Affiliation.Should().Be(expected.Affiliation);
        replayed.Species.Should().Be(expected.Species);
    }
}
