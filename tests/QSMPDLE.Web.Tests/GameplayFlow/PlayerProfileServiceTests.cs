using FluentAssertions;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Features.Statistics.Services;

namespace QSMPDLE.Web.Tests.GameplayFlow;

public sealed class PlayerProfileServiceTests
{
    private static readonly Guid PlayerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly InMemoryGameStatsStore gameStats = new();
    private readonly InMemoryPlayerStatsStore playerStats = new() { Stats = new PlayerStats { Id = PlayerId } };

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

        profile.DailyGamesPlayed.Should().Be(3);
        profile.DailyGamesWon.Should().Be(1);
        profile.DailyWinRate.Should().BeApproximately(100d / 3, 0.001);
        profile.GuessDistribution.Should().Equal(0, 1, 0, 0, 0, 0);
        profile.PracticeGamesStarted.Should().Be(2);
        profile.PracticeGamesFinished.Should().Be(1);
        profile.PracticeGamesWon.Should().Be(1);
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
        gameStats.CharacterStats = new PlayerCharacterStats
        {
            MostGuessedCharacterId = 7,
            MostGuessedCount = 4,
            MostCorrectlyGuessedCharacterId = 99,
            MostCorrectlyGuessedCount = 2,
        };

        var characterStats = (await CreateService().GetProfileAsync()).CharacterStats;

        characterStats.Should().NotBeNull();
        characterStats!.MostGuessedCharacterName.Should().Be("Alpha");
        characterStats.MostGuessedIconUrl.Should().NotBeNull();
        characterStats.MostGuessedCount.Should().Be(4);
        characterStats.MostCorrectlyGuessedCharacterName.Should().Be("99");
        characterStats.MostCorrectlyGuessedIconUrl.Should().BeNull();
    }

    [Fact]
    public async Task GetProfileAsync_NoCharacterHistory_LeavesNamesEmpty()
    {
        var characterStats = (await CreateService().GetProfileAsync()).CharacterStats;

        characterStats!.MostGuessedCharacterName.Should().BeNull();
        characterStats.MostCorrectlyGuessedCharacterName.Should().BeNull();
    }

    private PlayerProfileService CreateService()
    {
        var characters = new InMemoryCharacterStore([new Character { Id = 7, Name = "Alpha", IconUrl = "alpha.webp" }], 7);

        return new PlayerProfileService(playerStats, gameStats, new StubGlobalStatsService(), characters, new DayService());
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

    private sealed class StubGlobalStatsService : IGlobalStatsService
    {
        public Task<GlobalStatsView> GetGlobalStatsAsync() => Task.FromResult(new GlobalStatsView());
        public Task<List<DailyActivePlayersData>> GetDailyActivePlayersAsync(DateOnly? from) => throw new NotImplementedException();
        public Task<List<NewVsReturningPlayersData>> GetNewVsReturningPlayersAsync(DateOnly? from) => throw new NotImplementedException();
        public Task<PlayerActivityDistributionData[]> GetPlayerActivityDistributionAsync() => throw new NotImplementedException();
        public Task<GamesPerPlayerStats> GetGamesPerPlayerStatsAsync() => throw new NotImplementedException();
        public Task<RetentionStats> GetRetentionStatsAsync() => throw new NotImplementedException();
        public Task<GlobalCharacterStats> GetGlobalCharacterStatsAsync() => throw new NotImplementedException();
    }
}
