using FluentAssertions;
using QSMPDLE.Web.Extensions;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Tests.GameplayFlow;

public sealed class BinaryClueTests
{
    private static readonly IReadOnlySet<ClueGroup> AllClues = Enum.GetValues<ClueGroup>().ToHashSet();

    [Theory]
    [InlineData(true, true, ComparisonResult.Correct)]
    [InlineData(false, false, ComparisonResult.Correct)]
    [InlineData(true, false, ComparisonResult.Wrong)]
    [InlineData(false, true, ComparisonResult.Wrong)]
    public async Task CompareAsync_BinaryClues_MatchOnlyWhenEqual(bool targetFlag, bool guessedFlag,
        ComparisonResult expected)
    {
        // Arrange
        var target = CreateCharacter(1, "Target", targetFlag, targetFlag);
        var guessed = CreateCharacter(2, "Guessed", guessedFlag, guessedFlag);
        var comparer = new CharacterComparer(new InMemoryCharacterStore([target, guessed], target.Id));

        // Act
        var result = await comparer.CompareAsync(target.Id, guessed.Id, AllClues);

        // Assert
        result.QPhone.Should().Be(expected);
        result.Award2026.Should().Be(expected);
    }

    [Fact]
    public async Task StartGameAsync_LocalStateFromBeforeNewClues_RecomputesResults()
    {
        // Arrange: a saved practice guess deserialized without the new clues reads them as Wrong.
        var target = CreateCharacter(1, "Target", true, false);
        var guessed = CreateCharacter(2, "Guessed", true, false);
        var characterStore = new InMemoryCharacterStore([target, guessed], target.Id);
        var playerStatsStore = new InMemoryPlayerStatsStore { Stats = new PlayerStats { Id = Guid.NewGuid() } };
        var gameStateStore = new InMemoryGameStateStore();
        var comparer = new CharacterComparer(characterStore);
        var dayService = new DayService();
        var manager = new GameStateManager(gameStateStore, new GameService(characterStore, dayService), dayService,
            playerStatsStore, characterStore, comparer, new InMemoryGameStatsStore());

        var stale = await comparer.CompareAsync(target.Id, guessed.Id, AllClues);
        gameStateStore.State = new GameState
        {
            GameId = Guid.NewGuid(),
            PlayerId = playerStatsStore.Stats.Id,
            Game = new Game { TargetId = target.Id, PortraitUrl = string.Empty },
            GuessesMade = [new GuessResult { Character = stale.Character, Pronouns = stale.Pronouns }]
        };

        // Act
        var result = await manager.StartGameAsync(EntryPoint.Practice);

        // Assert
        result.Should().Be(LoadGameResult.LoadedExisting);
        var guess = manager.GameState.GuessesMade.Should().ContainSingle().Subject;
        guess.QPhone.Should().Be(ComparisonResult.Correct);
        guess.Award2026.Should().Be(ComparisonResult.Correct);
    }

    private static Character CreateCharacter(int id, string name, bool hasQPhone, bool wonAward) => new()
    {
        Id = id,
        Name = name,
        MinecraftUsername = name.ToLowerInvariant(),
        PortraitId = Guid.NewGuid(),
        Pronouns = ["He/Him"],
        LanguageCount = 1,
        Species = ["Human"],
        JoinDayNumber = 1,
        PhoneNumber = hasQPhone ? 1234 : null,
        WonAward2026 = wonAward
    };
}
