using FluentAssertions;
using QSMPDLE.Web.Features.Archive.Services;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Infrastructure.LocalStorage;

namespace QSMPDLE.Web.Tests.Infrastructure;

public sealed class StoredGamesTests
{
    // More than one message could name in full.
    private const int PlayedDays = 2000;

    private readonly FakeBrowser browser = new();

    [Fact]
    public async Task GetStoredDayNumbersAsync_ManyPlayedDays_ListsEveryDay()
    {
        for (var day = 1; day <= PlayedDays; day++)
            browser.Items[$"qsmpdle-canonical-{day}"] = "null";
        browser.Items["qsmpdle-first-game"] = "null";
        browser.Items["qsmpdle-canonical-old"] = "null";
        browser.Items["other-5000"] = "null";

        var days = await new ArchiveGameStateSource(browser.LocalStorage, browser).GetStoredDayNumbersAsync();

        days.Should().BeEquivalentTo(Enumerable.Range(1, PlayedDays));
    }

    [Theory]
    [InlineData("qsmpdle-practice-current", true)]
    [InlineData("qsmpdle-daily-7", true)]
    [InlineData("qsmpdle-archive-7", true)]
    [InlineData("qsmpdle-unrelated", false)]
    [InlineData("qsmpdle-canonical-1", false)]
    public async Task HasGuessedInOtherGameAsync_OneSavedGuess_IsFoundInAnyOtherGame(string key, bool expected)
    {
        var store = new LocalStorageGameStateStore(browser.LocalStorage, browser);
        await store.Init(key["qsmpdle-".Length..]);
        await store.SaveAsync(new GameState
        {
            GameId = Guid.NewGuid(),
            Game = new Game { TargetId = 1, PortraitUrl = "" },
            GuessesMade = [new GuessResult { Character = new CharacterLookup(1, "One", null, [], "") }],
        });
        await store.Init("canonical-1");

        (await store.HasGuessedInOtherGameAsync()).Should().Be(expected);
    }
}
