using FluentAssertions;
using Microsoft.Extensions.Options;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Features.Sharing;
using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Tests.GameplayFlow;

public sealed class ReleaseGatingTests
{
    private static readonly DateOnly Release = new(2026, 12, 1);
    private static readonly DateTimeOffset DayBeforeRelease = new(2026, 11, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ReleaseMidnight = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

    private const int TargetId = 1;
    private const int ReleasedGuessId = 2;
    private const int UnreleasedId = 60;

    [Fact]
    public void IsAvailableOn_ReleaseDate_IsInclusiveAndNullMeansAlways()
    {
        var unreleased = CreateCharacter(UnreleasedId, Release);
        var existing = CreateCharacter(TargetId);

        unreleased.IsAvailableOn(Release.AddDays(-1)).Should().BeFalse();
        unreleased.IsAvailableOn(Release).Should().BeTrue();
        existing.IsAvailableOn(DateOnly.MinValue).Should().BeTrue();
    }

    [Fact]
    public void GetActiveClues_AroundRelease_HidesGatedCluesUntilTheirDate()
    {
        var availability = CreateAvailability(new TestClock(DayBeforeRelease));

        availability.GetActiveClues(Release.AddDays(-1)).Should().BeEmpty();
        availability.GetActiveClues(Release).Should().BeEquivalentTo([ClueGroup.QPhone, ClueGroup.Award2026]);
    }

    [Fact]
    public void GetActiveClues_NoReleaseDatesConfigured_ShowsEveryClue()
    {
        var dayService = new DayService(new TestClock(DayBeforeRelease));
        var availability = new GameplayAvailabilityService(dayService, Options.Create(new GameplayOptions()));

        availability.GetActiveClues(DateOnly.MinValue).Should().BeEquivalentTo(Enum.GetValues<ClueGroup>());
    }

    [Fact]
    public void GetEffectiveDate_PracticeAndArchive_UseTodayAndThePuzzleDate()
    {
        var clock = new TestClock(DayBeforeRelease);
        var dayService = new DayService(clock);
        var availability = CreateAvailability(clock);
        var archiveDay = dayService.GetArchiveDayNumber(new DateOnly(2026, 11, 2));

        availability.GetEffectiveDate(null).Should().Be(new DateOnly(2026, 11, 30));
        availability.GetEffectiveDate(archiveDay).Should().Be(new DateOnly(2026, 11, 2));

        clock.Now = ReleaseMidnight;
        availability.GetEffectiveDate(null).Should().Be(Release);
    }

    [Fact]
    public async Task MakeGuessAsync_UnreleasedCharacter_IsRejectedWithoutTouchingStateOrStatistics()
    {
        var game = CreateGame(new TestClock(DayBeforeRelease));
        await game.Manager.StartGameAsync(EntryPoint.Daily);
        var savesBefore = game.GameStateStore.SavedStates.Count;

        var result = await game.Manager.MakeGuessAsync(UnreleasedId);

        result.Should().BeNull();
        game.Manager.GameState.GuessesMade.Should().BeEmpty();
        game.GameStateStore.SavedStates.Should().HaveCount(savesBefore);
        game.Statistics.Sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task MakeGuessAsync_UnknownCharacterId_IsRejected()
    {
        var game = CreateGame(new TestClock(DayBeforeRelease));
        await game.Manager.StartGameAsync(EntryPoint.Daily);

        var result = await game.Manager.MakeGuessAsync(999);

        result.Should().BeNull();
        game.Statistics.Sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task Daily_BeforeAndOnRelease_ClueResultsFollowThePuzzleDate()
    {
        var clock = new TestClock(DayBeforeRelease);
        var before = CreateGame(clock);
        await before.Manager.StartGameAsync(EntryPoint.Daily);
        var beforeGuess = await before.Manager.MakeGuessAsync(ReleasedGuessId);

        clock.Now = ReleaseMidnight;
        var after = CreateGame(clock);
        await after.Manager.StartGameAsync(EntryPoint.Daily);
        var afterGuess = await after.Manager.MakeGuessAsync(UnreleasedId);

        before.Manager.ActiveClues.Should().BeEmpty();
        beforeGuess!.QPhone.Should().BeNull();
        beforeGuess.Award2026.Should().BeNull();

        after.Manager.ActiveClues.Should().BeEquivalentTo([ClueGroup.QPhone, ClueGroup.Award2026]);
        afterGuess.Should().NotBeNull("the character is released on the puzzle's date");
        afterGuess!.QPhone.Should().NotBeNull();
        afterGuess.Award2026.Should().NotBeNull();
    }

    [Fact]
    public async Task Daily_CircuitOutlivesReleaseMidnight_KeepsThePuzzleDate()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 11, 30, 23, 59, 0, TimeSpan.Zero));
        var game = CreateGame(clock);
        await game.Manager.StartGameAsync(EntryPoint.Daily);

        clock.Now = ReleaseMidnight.AddMinutes(1);
        var unreleased = await game.Manager.MakeGuessAsync(UnreleasedId);
        var released = await game.Manager.MakeGuessAsync(ReleasedGuessId);

        game.Manager.EffectiveDate.Should().Be(new DateOnly(2026, 11, 30));
        game.Manager.ActiveClues.Should().BeEmpty();
        unreleased.Should().BeNull();
        released!.QPhone.Should().BeNull();
    }

    [Fact]
    public async Task ArchiveReplay_PreReleaseDayAfterRelease_UsesThatDaysContent()
    {
        var clock = new TestClock(ReleaseMidnight.AddDays(4));
        var game = CreateGame(clock);
        var archiveDay = game.DayService.GetArchiveDayNumber(new DateOnly(2026, 11, 25));
        game.Statistics.Sessions[Guid.NewGuid()] = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = game.PlayerId,
            SessionCategory = SessionCategory.CanonicalPuzzle,
            PuzzleId = archiveDay,
            TargetCharacterId = TargetId,
            StartedOnUtc = DayBeforeRelease,
            Guesses = [new GameGuess { GuessOrder = 0, GuessedCharacterId = ReleasedGuessId }],
        };

        var loaded = await game.Manager.StartGameAsync(EntryPoint.Archive, archiveDay);
        var unreleased = await game.Manager.MakeGuessAsync(UnreleasedId);
        var share = ShareText.BuildDailyResult(archiveDay, game.Manager.GameState.GuessesMade);

        loaded.Should().Be(Extensions.LoadGameResult.LoadedExisting);
        game.Manager.ActiveClues.Should().BeEmpty();
        var restored = game.Manager.GameState.GuessesMade.Should().ContainSingle().Subject;
        restored.QPhone.Should().BeNull();
        restored.Award2026.Should().BeNull();
        unreleased.Should().BeNull();
        SquaresIn(share.Split('\n')[2]).Should().Be(5);
    }

    [Fact]
    public async Task ArchiveReplay_LocalStateWithGatedResults_IsRecomputedForThePuzzleDate()
    {
        var clock = new TestClock(ReleaseMidnight.AddDays(4));
        var game = CreateGame(clock);
        var archiveDay = game.DayService.GetArchiveDayNumber(new DateOnly(2026, 11, 25));
        game.GameStateStore.State = new GameState
        {
            GameId = Guid.NewGuid(),
            PlayerId = game.PlayerId,
            Game = new Game { PuzzleId = archiveDay, TargetId = TargetId, PortraitUrl = string.Empty },
            GuessesMade = [new GuessResult { Character = new CharacterLookup(ReleasedGuessId, "Guess", null, [], ""), QPhone = ComparisonResult.Correct, Award2026 = ComparisonResult.Wrong }],
        };

        await game.Manager.StartGameAsync(EntryPoint.Archive, archiveDay);

        var restored = game.Manager.GameState.GuessesMade.Should().ContainSingle().Subject;
        restored.QPhone.Should().BeNull();
        restored.Award2026.Should().BeNull();
    }

    [Fact]
    public async Task MakeGuessAsync_AroundThePremiere_RecordsTheRulesetOfTheGamesDate()
    {
        var clock = new TestClock(ReleaseMidnight.AddSeconds(-1));
        var lastClassicDaily = CreateGame(clock);
        await lastClassicDaily.Manager.StartGameAsync(EntryPoint.Daily);
        var lastClassicPractice = CreateGame(clock);
        await lastClassicPractice.Manager.StartGameAsync(EntryPoint.Practice);
        await lastClassicPractice.Manager.MakeGuessAsync(ReleasedGuessId);

        clock.Now = ReleaseMidnight;
        var openedBeforeMidnight = lastClassicDaily;
        await openedBeforeMidnight.Manager.MakeGuessAsync(ReleasedGuessId);
        var firstDaily = CreateGame(clock);
        await firstDaily.Manager.StartGameAsync(EntryPoint.Daily);
        await firstDaily.Manager.MakeGuessAsync(ReleasedGuessId);
        var firstPractice = CreateGame(clock);
        await firstPractice.Manager.StartGameAsync(EntryPoint.Practice);
        await firstPractice.Manager.MakeGuessAsync(ReleasedGuessId);
        var archive = CreateGame(clock);
        await archive.Manager.StartGameAsync(EntryPoint.Archive, archive.DayService.GetArchiveDayNumber(Release.AddDays(-5)));
        await archive.Manager.MakeGuessAsync(ReleasedGuessId);
        await lastClassicPractice.Manager.MakeGuessAsync(TargetId);

        RulesetOf(openedBeforeMidnight).Should().Be(Ruleset.Classic, "the puzzle keeps its own date");
        RulesetOf(lastClassicPractice).Should().Be(Ruleset.Classic, "a game keeps the ruleset of its first guess");
        RulesetOf(firstDaily).Should().Be(Ruleset.V2);
        RulesetOf(firstPractice).Should().Be(Ruleset.V2);
        RulesetOf(archive).Should().Be(Ruleset.Classic, "an earlier puzzle is played with the earlier rules");
    }

    private static Ruleset RulesetOf(TestGame game) => game.Statistics.Sessions.Values.Single().Ruleset;

    [Fact]
    public async Task StartPractice_BeforeRelease_NeverPicksAnUnreleasedTarget()
    {
        var character = CreateCharacter(UnreleasedId, Release);
        var store = new InMemoryCharacterStore([CreateCharacter(TargetId), character], randomCharacterId: UnreleasedId);
        var service = new GameService(store, new DayService(new TestClock(DayBeforeRelease)));

        var practice = await service.StartPracticeAsync();

        practice.Game.TargetId.Should().Be(TargetId);
    }

    [Fact]
    public void BuildDailyResult_ClueSet_SquaresMatchActiveCluesInColumnOrder()
    {
        var lookup = new CharacterLookup(ReleasedGuessId, "Guess", null, [], "");
        GuessResult withoutGated = new() { Character = lookup, Pronouns = ComparisonResult.Correct, Species = ComparisonResult.Wrong, Languages = ComparisonResult.More, Joined = ComparisonResult.Later, Affiliation = ComparisonResult.Partial };
        GuessResult withGated = new() { Character = lookup, Pronouns = ComparisonResult.Correct, Species = ComparisonResult.Wrong, Languages = ComparisonResult.More, Joined = ComparisonResult.Later, QPhone = ComparisonResult.Wrong, Award2026 = ComparisonResult.Correct, Affiliation = ComparisonResult.Partial };

        var before = ShareText.BuildDailyResult(1, [withoutGated]).Split('\n')[2].Trim();
        var after = ShareText.BuildDailyResult(1, [withGated]).Split('\n')[2].Trim();

        before.Should().Be("🟩🟥⬆️➡️🟨");
        after.Should().Be("🟩🟥⬆️➡️🟥🟩🟨");
    }

    private static int SquaresIn(string row) =>
        System.Globalization.StringInfo.ParseCombiningCharacters(row.Trim()).Length;

    private static GameplayAvailabilityService CreateAvailability(TimeProvider clock) =>
        new(new DayService(clock), Options.Create(new GameplayOptions
        {
            CluePremieres = { [ClueGroup.QPhone] = Release, [ClueGroup.Award2026] = Release },
        }));

    private static TestGame CreateGame(TestClock clock)
    {
        var dayService = new DayService(clock);
        Character[] characters = [CreateCharacter(TargetId), CreateCharacter(ReleasedGuessId), CreateCharacter(UnreleasedId, Release)];
        var store = new InMemoryCharacterStore(characters, TargetId);
        var playerId = Guid.NewGuid();
        var playerStatsStore = new InMemoryPlayerStatsStore { Stats = new PlayerStats { Id = playerId } };
        var gameStateStore = new InMemoryGameStateStore();
        var statistics = new InMemoryGameStatsStore();
        var manager = new GameStateManager(
            gameStateStore,
            new GameService(store, dayService),
            dayService,
            playerStatsStore,
            store,
            new CharacterComparer(store),
            statistics,
            availability: CreateAvailability(clock));

        return new TestGame(manager, gameStateStore, statistics, dayService, playerId);
    }

    private static Character CreateCharacter(int id, DateOnly? availableFrom = null) => new()
    {
        Id = id,
        Name = $"Character {id}",
        MinecraftUsername = $"character{id}",
        PortraitId = Guid.NewGuid(),
        Pronouns = ["He/Him"],
        LanguageCount = 1,
        Species = ["Human"],
        JoinDayNumber = id,
        AvailableFrom = availableFrom,
    };

    private sealed record TestGame(GameStateManager Manager, InMemoryGameStateStore GameStateStore, InMemoryGameStatsStore Statistics, DayService DayService, Guid PlayerId);
}
