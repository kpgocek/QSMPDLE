using FluentAssertions;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Features.Statistics.Services;

namespace QSMPDLE.Web.Tests.GameplayFlow;

public sealed class GameStateManagerArchiveReplayTests
{
    [Fact]
    public async Task ArchiveReplay_UsesCharacterComparerResults()
    {
        // Arrange - create store/service/fakes like existing tests
        var target = CreateCharacter(1, "Target", joinDay: 10, languages: 2, pronouns: new[] { "Any" }, affiliations: new[] { "Guild" }, species: new[] { "Human" });
        var guessOne = CreateCharacter(2, "GuessOne", joinDay: 20, languages: 4, pronouns: new[] { "He/Him" }, affiliations: new[] { "Other" }, species: new[] { "Unknown" });
        var characters = new[] { target, guessOne };
        var characterStore = new InMemoryCharacterStore(characters, target.Id);

        var playerStatsStore = new InMemoryPlayerStatsStore { Stats = new PlayerStats { Id = Guid.NewGuid() } };
        var gameStateStore = new InMemoryGameStateStore();
        var gameStatsStore = new InMemoryGameStatsStore();
        var statisticsService = new StatisticsService(playerStatsStore, gameStatsStore);
        var dayService = new DayService();
        var comparer = new CharacterComparer(characterStore);

        var manager = new GameStateManager(gameStateStore, new GameService(characterStore, dayService), dayService, playerStatsStore, characterStore, comparer, statisticsService);

        // Create a completed daily session in the stats store
        var completed = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = playerStatsStore.Stats.Id,
            Mode = GameMode.Daily,
            DailyNumber = 5,
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
        var result = await manager.StartGameAsync(GameMode.Archive, 5);

        // Assert
        result.Should().Be(Extensions.LoadGameResult.LoadedExisting);
        manager.GameState.Should().NotBeNull();
        manager.GameState.GuessesMade.Should().HaveCount(1);

        var replayed = manager.GameState.GuessesMade[0];

        var expected = await comparer.CompareAsync(target.Id, guessOne.Id);

        // Compare fields produced by comparer
        replayed.Character.Id.Should().Be(expected.Character.Id);
        replayed.IsCorrect.Should().Be(expected.IsCorrect);
        replayed.Pronouns.Should().Be(expected.Pronouns);
        replayed.Languages.Should().Be(expected.Languages);
        replayed.Joined.Should().Be(expected.Joined);
        replayed.Affiliation.Should().Be(expected.Affiliation);
        replayed.Species.Should().Be(expected.Species);
    }

    private static Character CreateCharacter(int id, string name, int joinDay, int languages, string[] pronouns, string[] affiliations, string[] species) => new()
    {
        Id = id,
        Name = name,
        MinecraftUsername = name.ToLowerInvariant(),
        Aliases = new List<string>(),
        IconUrl = $"https://example.com/{id}.png",
        Pronouns = pronouns.ToList(),
        Languages = languages,
        Affiliations = affiliations.ToList(),
        Species = species.ToList(),
        JoinDayNumber = joinDay,
    };
}
