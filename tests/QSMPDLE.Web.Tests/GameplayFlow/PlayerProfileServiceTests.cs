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

    private readonly InMemoryGameStatsStore gameStats = new();
    private readonly InMemoryPlayerStatsStore playerStats = new() { Stats = new PlayerStats { Id = PlayerId } };
    private readonly InMemoryHiLoScoreStore hiLoScores = new();
    private readonly DayService dayService = new(new TestClock(Now));

    private int TodayPuzzle => dayService.GetTodayDayNumber();

    [Fact]
    public async Task GetProfileAsync_MixedSessions_SummarizesCanonicalAndPracticeSeparately()
    {
        AddSession(SessionCategory.CanonicalPuzzle, isWon: true, guesses: 2, finished: true);
        AddSession(SessionCategory.CanonicalPuzzle, isWon: false, guesses: 6, finished: true);
        AddSession(SessionCategory.CanonicalPuzzle, isWon: false, guesses: 1, finished: false);
        AddSession(SessionCategory.CanonicalPuzzle, isWon: true, guesses: 1, finished: true, isLegacyDuplicate: true);
        AddSession(SessionCategory.Practice, isWon: true, guesses: 3, finished: true);
        AddSession(SessionCategory.Practice, isWon: false, guesses: 1, finished: false);

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
        AddGame(target: 9, isWon: false, guessed: [1, 9], finished: false);
        AddGame(target: 9, isWon: false, guessed: [1, 9], category: SessionCategory.Practice, finished: false);
        AddGame(target: 9, isWon: false, guessed: [1, 2, 3, 4, 5, 6], category: SessionCategory.Practice, finished: false);

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
        AddSession(SessionCategory.CanonicalPuzzle, isWon: true, guesses: 1, finished: true);

        await CreateService().GetProfileAsync();

        gameStats.PlayerGamesLoadCount.Should().Be(1);
        playerStats.LoadCount.Should().Be(0);
    }

    [Fact]
    public async Task GetProfileAsync_CharacterMissingFromCatalog_FallsBackToItsId()
    {
        for (var game = 0; game < 4; game++)
            AddGame(target: 99, isWon: false, guessed: [7]);
        AddGame(target: 99, isWon: true, guessed: [99]);
        AddGame(target: 99, isWon: true, guessed: [99]);

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
        AddGame(target: 3, isWon: false, guessed: [5, 3]);
        AddGame(target: 4, isWon: true, guessed: [5, 3, 4]);
        AddGame(target: 3, isWon: true, guessed: [3]);
        AddGame(target: 3, isWon: true, guessed: [3]);
        AddGame(target: 1, isWon: false, guessed: [6], category: SessionCategory.Practice);
        AddGame(target: 1, isWon: false, guessed: [6], category: SessionCategory.Practice);
        AddGame(target: 1, isWon: false, guessed: [6], category: SessionCategory.Practice);

        var characterStats = (await CreateService().GetProfileAsync()).CharacterStats!;

        characterStats.MostGuessedCharacterId.Should().Be(5, "guesses of the target and practice guesses do not count");
        characterStats.MostGuessedCount.Should().Be(2);
    }

    [Fact]
    public async Task GetProfileAsync_MostCorrectlyGuessed_CountsWonCanonicalGamesByTarget()
    {
        AddGame(target: 8, isWon: true, guessed: [8]);
        AddGame(target: 8, isWon: true, guessed: [2, 8]);
        AddGame(target: 9, isWon: true, guessed: [9]);
        AddGame(target: 9, isWon: false, guessed: [1]);
        AddGame(target: 9, isWon: false, guessed: [2]);
        AddGame(target: 9, isWon: true, guessed: [9], category: SessionCategory.Practice);
        AddGame(target: 9, isWon: true, guessed: [9], category: SessionCategory.Practice);

        var characterStats = (await CreateService().GetProfileAsync()).CharacterStats!;

        characterStats.MostCorrectlyGuessedCharacterId.Should().Be(8);
        characterStats.MostCorrectlyGuessedCount.Should().Be(2);
    }

    [Fact]
    public async Task GetProfileAsync_TodaysPuzzle_IsExcludedFromCharacterStats()
    {
        AddGame(target: 9, isWon: true, guessed: [5, 9]);
        AddGame(target: 8, isWon: true, guessed: [6, 8], puzzleId: TodayPuzzle);
        AddGame(target: 8, isWon: true, guessed: [6, 8], puzzleId: TodayPuzzle);

        var characterStats = (await CreateService().GetProfileAsync()).CharacterStats!;

        characterStats.MostGuessedCharacterId.Should().Be(5);
        characterStats.MostGuessedCount.Should().Be(1);
        characterStats.MostCorrectlyGuessedCharacterId.Should().Be(9);
        characterStats.MostCorrectlyGuessedCount.Should().Be(1);
    }

    [Fact]
    public async Task GetProfileAsync_LegacyDuplicates_AreExcludedFromCharacterStats()
    {
        AddGame(target: 9, isWon: true, guessed: [5, 9]);
        AddGame(target: 8, isWon: true, guessed: [6, 8], isLegacyDuplicate: true);
        AddGame(target: 8, isWon: true, guessed: [6, 8], isLegacyDuplicate: true);

        var characterStats = (await CreateService().GetProfileAsync()).CharacterStats!;

        characterStats.MostGuessedCharacterId.Should().Be(5);
        characterStats.MostCorrectlyGuessedCharacterId.Should().Be(9);
    }

    [Fact]
    public async Task GetProfileAsync_TiedCounts_PickTheLowestCharacterId()
    {
        AddGame(target: 9, isWon: true, guessed: [6, 9]);
        AddGame(target: 8, isWon: true, guessed: [6, 5, 8]);
        AddGame(target: 9, isWon: true, guessed: [5, 9]);
        AddGame(target: 8, isWon: true, guessed: [8]);

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
        var characters = new InMemoryCharacterStore([new Character { Id = 7, Name = "Alpha", PortraitId = Guid.NewGuid() }], 7);

        return new PlayerProfileService(playerStats, gameStats, hiLoScores, characters, dayService);
    }

    private void AddSession(SessionCategory category, bool isWon, int guesses, bool finished, bool isLegacyDuplicate = false)
    {
        var session = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = PlayerId,
            SessionCategory = category,
            IsWon = isWon,
            FinishedOnUtc = finished ? DateTimeOffset.UtcNow : null,
            IsLegacyDuplicate = isLegacyDuplicate,
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
            IsLegacyDuplicate = isLegacyDuplicate,
        };

        foreach (var characterId in guessed)
            session.AddGuess(characterId);

        gameStats.Sessions[session.GameId] = session;
    }
}
