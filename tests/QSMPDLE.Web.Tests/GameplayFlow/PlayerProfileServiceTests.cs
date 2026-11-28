using FluentAssertions;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Features.Statistics.Services;

namespace QSMPDLE.Web.Tests.GameplayFlow;

public sealed class PlayerProfileServiceTests
{
    private static readonly Guid PlayerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
    private readonly DayService dayService = new(new TestClock(Now));

    private readonly InMemoryGameStatsStore gameStats = new();
    private readonly InMemoryHiLoScoreStore hiLoScores = new();
    private readonly InMemoryPlayerStatsStore playerStats = new() { Stats = new PlayerStats { Id = PlayerId } };

    private int TodayPuzzle => dayService.GetTodayDayNumber();

    [Fact]
    public async Task GetProfileAsync_MixedSessions_SummarizesCanonicalAndPracticeSeparately()
    {
        AddSession(SessionCategory.CanonicalPuzzle, true, 2, true);
        AddSession(SessionCategory.CanonicalPuzzle, false, 6, true);
        AddSession(SessionCategory.CanonicalPuzzle, false, 1, false);
        AddSession(SessionCategory.CanonicalPuzzle, true, 1, true, true);
        AddSession(SessionCategory.Practice, true, 3, true);
        AddSession(SessionCategory.Practice, false, 1, false);

        var profile = await CreateService().GetProfileAsync();

        profile.Stats.GamesPlayed.Should().Be(2, "an unfinished game is not played yet");
        profile.Stats.GamesWon.Should().Be(1);
        profile.Stats.WinRate.Should().Be(50);
        profile.Stats.GuessDistribution.Should().Equal(0, 1, 0, 0, 0, 0);
        profile.PracticeGamesStarted.Should().Be(2);
        profile.PracticeGamesFinished.Should().Be(1);
        profile.PracticeGamesWon.Should().Be(1);
    }

    [Fact]
    public async Task GetProfileAsync_GamesDecidedByTheirGuesses_CountWithoutASavedFinish()
    {
        AddGame(9, false, [1, 9], finished: false);
        AddGame(9, false, [1, 9], SessionCategory.Practice, finished: false);
        AddGame(9, false, [1, 2, 3, 4, 5, 6], SessionCategory.Practice, finished: false);

        var profile = await CreateService().GetProfileAsync();

        profile.Stats.GamesWon.Should().Be(1);
        profile.Stats.GuessDistribution.Should().Equal(0, 1, 0, 0, 0, 0);
        profile.PracticeGamesFinished.Should().Be(2);
        profile.PracticeGamesWon.Should().Be(1);
        profile.CharacterStats!.MostCorrectlyGuessedCount.Should().Be(1);
    }

    [Fact]
    public async Task GetProfileAsync_LoadsPlayerHistoryOnce()
    {
        AddSession(SessionCategory.CanonicalPuzzle, true, 1, true);

        await CreateService().GetProfileAsync();

        gameStats.PlayerGamesLoadCount.Should().Be(1);
        playerStats.LoadCount.Should().Be(0);
    }

    [Fact]
    public async Task GetProfileAsync_CharacterMissingFromCatalog_FallsBackToItsId()
    {
        for (var game = 0; game < 4; game++)
        {
            AddGame(99, false, [7]);
        }

        AddGame(99, true, [99]);
        AddGame(99, true, [99]);

        var characterStats = (await CreateService().GetProfileAsync()).CharacterStats;

        characterStats.Should().NotBeNull();
        characterStats!.MostGuessedCharacterName.Should().Be("Alpha");
        characterStats.MostGuessedPortraitFolder.Should().NotBeNull();
        characterStats.MostGuessedCount.Should().Be(4);
        characterStats.MostCorrectlyGuessedCharacterName.Should().Be("99");
        characterStats.MostCorrectlyGuessedPortraitFolder.Should().BeNull();
    }

    [Fact]
    public async Task GetProfileAsync_NoCharacterHistory_LeavesNamesEmpty()
    {
        var characterStats = (await CreateService().GetProfileAsync()).CharacterStats;

        characterStats!.MostGuessedCharacterId.Should().BeNull();
        characterStats.MostGuessedCharacterName.Should().BeNull();
        characterStats.MostGuessedCount.Should().Be(0);
        characterStats.MostCorrectlyGuessedCharacterId.Should().BeNull();
        characterStats.MostCorrectlyGuessedCharacterName.Should().BeNull();
        characterStats.MostCorrectlyGuessedCount.Should().Be(0);
    }

    [Fact]
    public async Task GetProfileAsync_MostGuessed_CountsOnlyWrongGuessesInCanonicalGames()
    {
        AddGame(3, false, [5, 3]);
        AddGame(4, true, [5, 3, 4]);
        AddGame(3, true, [3]);
        AddGame(3, true, [3]);
        AddGame(1, false, [6], SessionCategory.Practice);
        AddGame(1, false, [6], SessionCategory.Practice);
        AddGame(1, false, [6], SessionCategory.Practice);

        var characterStats = (await CreateService().GetProfileAsync()).CharacterStats!;

        characterStats.MostGuessedCharacterId.Should().Be(5, "guesses of the target and practice guesses do not count");
        characterStats.MostGuessedCount.Should().Be(2);
    }

    [Fact]
    public async Task GetProfileAsync_MostCorrectlyGuessed_CountsWonCanonicalGamesByTarget()
    {
        AddGame(8, true, [8]);
        AddGame(8, true, [2, 8]);
        AddGame(9, true, [9]);
        AddGame(9, false, [1]);
        AddGame(9, false, [2]);
        AddGame(9, true, [9], SessionCategory.Practice);
        AddGame(9, true, [9], SessionCategory.Practice);

        var characterStats = (await CreateService().GetProfileAsync()).CharacterStats!;

        characterStats.MostCorrectlyGuessedCharacterId.Should().Be(8);
        characterStats.MostCorrectlyGuessedCount.Should().Be(2);
    }

    [Fact]
    public async Task GetProfileAsync_TodaysPuzzle_IsExcludedFromCharacterStats()
    {
        AddGame(9, true, [5, 9]);
        AddGame(8, true, [6, 8], puzzleId: TodayPuzzle);
        AddGame(8, true, [6, 8], puzzleId: TodayPuzzle);

        var characterStats = (await CreateService().GetProfileAsync()).CharacterStats!;

        characterStats.MostGuessedCharacterId.Should().Be(5);
        characterStats.MostGuessedCount.Should().Be(1);
        characterStats.MostCorrectlyGuessedCharacterId.Should().Be(9);
        characterStats.MostCorrectlyGuessedCount.Should().Be(1);
    }

    [Fact]
    public async Task GetProfileAsync_LegacyDuplicates_AreExcludedFromCharacterStats()
    {
        AddGame(9, true, [5, 9]);
        AddGame(8, true, [6, 8], isLegacyDuplicate: true);
        AddGame(8, true, [6, 8], isLegacyDuplicate: true);

        var characterStats = (await CreateService().GetProfileAsync()).CharacterStats!;

        characterStats.MostGuessedCharacterId.Should().Be(5);
        characterStats.MostCorrectlyGuessedCharacterId.Should().Be(9);
    }

    [Fact]
    public async Task GetProfileAsync_TiedCounts_PickTheLowestCharacterId()
    {
        AddGame(9, true, [6, 9]);
        AddGame(8, true, [6, 5, 8]);
        AddGame(9, true, [5, 9]);
        AddGame(8, true, [8]);

        var characterStats = (await CreateService().GetProfileAsync()).CharacterStats!;

        characterStats.MostGuessedCharacterId.Should().Be(5);
        characterStats.MostGuessedCount.Should().Be(2);
        characterStats.MostCorrectlyGuessedCharacterId.Should().Be(8);
        characterStats.MostCorrectlyGuessedCount.Should().Be(2);
    }

    [Fact]
    public async Task GetProfileAsync_HiLoScore_IsThePlayersBest()
    {
        await hiLoScores.RecordScoreAsync(PlayerId, 12);
        await hiLoScores.RecordScoreAsync(Guid.NewGuid(), 40);

        (await CreateService().GetProfileAsync()).HiLoBestScore.Should().Be(12);
    }

    private PlayerProfileService CreateService()
    {
        var characters =
            new InMemoryCharacterStore([new Character { Id = 7, Name = "Alpha", PortraitId = Guid.NewGuid() }], 7);

        return new PlayerProfileService(playerStats, gameStats, hiLoScores, characters, dayService);
    }

    private void AddSession(SessionCategory category, bool isWon, int guesses, bool finished,
        bool isLegacyDuplicate = false)
    {
        var session = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = PlayerId,
            SessionCategory = category,
            IsWon = isWon,
            FinishedOnUtc = finished ? DateTimeOffset.UtcNow : null,
            IsLegacyDuplicate = isLegacyDuplicate
        };

        for (var guess = 0; guess < guesses; guess++)
        {
            session.AddGuess(guess + 1);
        }

        gameStats.Sessions[session.GameId] = session;
    }

    private void AddGame(
        int target,
        bool isWon,
        int[] guessed,
        SessionCategory category = SessionCategory.CanonicalPuzzle,
        int? puzzleId = null,
        bool isLegacyDuplicate = false,
        DateTimeOffset? startedOnUtc = null,
        bool finished = true)
    {
        var session = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = PlayerId,
            SessionCategory = category,
            PuzzleId = puzzleId ?? TodayPuzzle - 1,
            TargetCharacterId = target,
            IsWon = isWon,
            StartedOnUtc = startedOnUtc ?? Now.AddDays(-1),
            FinishedOnUtc = finished ? Now.AddDays(-1) : null,
            IsLegacyDuplicate = isLegacyDuplicate
        };

        foreach (var characterId in guessed)
        {
            session.AddGuess(characterId);
        }

        gameStats.Sessions[session.GameId] = session;
    }
}
