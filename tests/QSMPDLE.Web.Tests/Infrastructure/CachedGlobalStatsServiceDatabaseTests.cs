using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Features.Statistics.Services;
using QSMPDLE.Web.Infrastructure.Persistence;
using QSMPDLE.Web.Tests.GameplayFlow;
using Testcontainers.PostgreSql;

namespace QSMPDLE.Web.Tests.Infrastructure;

public sealed class CachedGlobalStatsServiceDatabaseTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18").Build();
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;

        await using var database = new ApplicationDbContext(options);
        await database.Database.MigrateAsync();
    }

    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    [Fact]
    public async Task GetTotalsAsync_MixedSessions_CountsFinishedGamesAndDistinctPlayers()
    {
        var returning = Guid.NewGuid();
        var abandoning = Guid.NewGuid();
        await SeedAsync(
            Session(returning, SessionCategory.CanonicalPuzzle, true),
            Session(returning, SessionCategory.Practice, true),
            Session(abandoning, SessionCategory.Practice, false),
            Session(Guid.NewGuid(), SessionCategory.CanonicalPuzzle, true, true));
        var service = new CachedGlobalStatsService(
            new TestDbContextFactory(options),
            new MemoryCache(new MemoryCacheOptions()),
            new TestClock(DateTimeOffset.UtcNow));

        var totals = await service.GetTotalsAsync();

        totals.Should().Be(new GlobalTotals(2, 1),
            "abandoned games and legacy duplicates are not games played, and a player who only abandoned is not counted");
    }

    [Fact]
    public async Task GetTotalsAsync_NoGames_IsZero()
    {
        var service = new CachedGlobalStatsService(
            new TestDbContextFactory(options),
            new MemoryCache(new MemoryCacheOptions()),
            new TestClock(DateTimeOffset.UtcNow));

        (await service.GetTotalsAsync()).Should().Be(new GlobalTotals(0, 0));
    }

    [Fact]
    public async Task GetTotalsAsync_GamesDecidedByTheirGuesses_CountWithoutASavedFinish()
    {
        await SeedAsync(
            Guessed(100, 1, 2, 3, 4, 5, 6),
            Guessed(100, 1, 100),
            Guessed(100, 1, 2, 3, 4, 5),
            Guessed(0, 1, 2, 3, 4, 5, 6));
        var service = new CachedGlobalStatsService(
            new TestDbContextFactory(options),
            new MemoryCache(new MemoryCacheOptions()),
            new TestClock(DateTimeOffset.UtcNow));

        var totals = await service.GetTotalsAsync();

        totals.Should().Be(new GlobalTotals(2, 2),
            "an unfinished board, or one with an unknown answer, is not decided");
    }

    [Fact]
    public async Task RecordScoreAsync_LowerScore_NeverReplacesTheBest()
    {
        var scores = new DatabaseHiLoScoreStore(new TestDbContextFactory(options));
        var player = Guid.NewGuid();

        await scores.RecordScoreAsync(player, 9);
        await scores.RecordScoreAsync(player, 4);
        await scores.RecordScoreAsync(Guid.NewGuid(), 6);

        (await scores.GetBestScoreAsync(player)).Should().Be(9, "a lower score must not replace the best");
        (await scores.GetBestScoreAsync(Guid.NewGuid())).Should().Be(0);

        await scores.RecordScoreAsync(player, 10);
        (await scores.GetBestScoreAsync(player)).Should().Be(10);
    }

    private static GameSession Guessed(int target, params int[] guessedCharacterIds)
    {
        var session = Session(Guid.NewGuid(), SessionCategory.CanonicalPuzzle, false);
        session.TargetCharacterId = target;
        foreach (var characterId in guessedCharacterIds)
        {
            session.AddGuess(characterId);
        }

        return session;
    }

    private async Task SeedAsync(params GameSession[] sessions)
    {
        await using var database = new ApplicationDbContext(options);
        database.GameStats.AddRange(sessions);
        await database.SaveChangesAsync();
    }

    private static GameSession
        Session(Guid playerId, SessionCategory category, bool finished, bool isLegacyDuplicate = false) => new()
    {
        GameId = Guid.NewGuid(),
        PlayerId = playerId,
        SessionCategory = category,
        TargetCharacterId = 1,
        IsLegacyDuplicate = isLegacyDuplicate,
        StartedOnUtc = DateTimeOffset.UtcNow,
        FinishedOnUtc = finished ? DateTimeOffset.UtcNow : null
    };

    private sealed class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);
    }
}
