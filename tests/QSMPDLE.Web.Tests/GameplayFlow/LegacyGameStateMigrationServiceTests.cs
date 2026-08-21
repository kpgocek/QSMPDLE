using FluentAssertions;
using System.Text.Json;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Infrastructure.LocalStorage;

namespace QSMPDLE.Web.Tests.GameplayFlow;

public sealed class LegacyGameStateMigrationServiceTests
{
    [Fact]
    public void ReadsTheCamelCaseDayNumberUsedByLegacyBrowserStates()
    {
        const string json = """{"dayNumber": 17, "TargetId": 1, "PortraitUrl": "portrait"}""";

        var game = JsonSerializer.Deserialize<Game>(json);

        game.Should().NotBeNull();
        game!.PuzzleId.Should().Be(17);
    }

    [Fact]
    public async Task MigratesEveryLegacyPuzzleAndRemovesAllLegacyKeys()
    {
        var store = new InMemoryGameStateStore();
        store.SetStateForKey("daily-4", CreateState(4, GameMode.Daily, guesses: 1));
        store.SetStateForKey("archive-4", CreateState(4, GameMode.Archive, guesses: 2));
        store.SetStateForKey("daily-9", CreateState(9, GameMode.Daily, won: true, guesses: 3));
        var service = new LegacyGameStateMigrationService(store);

        await service.MigrateAsync();

        store.HasStateForKey("daily-4").Should().BeFalse();
        store.HasStateForKey("archive-4").Should().BeFalse();
        store.HasStateForKey("daily-9").Should().BeFalse();

        var puzzle4 = await store.GetByKeyAsync("canonical-4");
        puzzle4.Should().NotBeNull();
        puzzle4!.GuessesMade.Should().HaveCount(2);
        puzzle4.EntryPoint.Should().Be(EntryPoint.Archive);
        puzzle4.SessionCategory.Should().Be(SessionCategory.CanonicalPuzzle);

        var puzzle9 = await store.GetByKeyAsync("canonical-9");
        puzzle9.Should().NotBeNull();
        puzzle9!.IsWon.Should().BeTrue();
        puzzle9.Game.PuzzleId.Should().Be(9);
    }

    private static GameState CreateState(int puzzleId, GameMode mode, bool won = false, int guesses = 0)
    {
        var state = new GameState
        {
            GameId = Guid.NewGuid(),
            Game = new Game { PuzzleId = puzzleId, TargetId = 1, PortraitUrl = string.Empty },
            GameMode = mode,
            IsWon = won,
        };

        for (var index = 0; index < guesses; index++)
        {
            state.GuessesMade.Add(new GuessResult
            {
                Character = new CharacterLookup(index + 10, $"Guess {index}", null, [], string.Empty),
            });
        }

        return state;
    }
}
