using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace QSMPDLE.Web.Tests.Infrastructure;

public sealed class DatabaseGameStatsStoreTests : IAsyncLifetime
{
    private static readonly Guid PlayerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

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
    public async Task GetPlayerDailyGamesByNumberRangeAsync_LegacyDuplicate_IsExcluded()
    {
        var kept = CreateDailySession(isLegacyDuplicate: false);
        await using (var database = new ApplicationDbContext(options))
        {
            database.GameStats.AddRange(kept, CreateDailySession(isLegacyDuplicate: true));
            await database.SaveChangesAsync();
        }

        var store = new DatabaseGameStatsStore(new TestDbContextFactory(options));

        var sessions = await store.GetPlayerDailyGamesByNumberRangeAsync(PlayerId, 1, 10);

        sessions.Should().ContainSingle().Which.GameId.Should().Be(kept.GameId);
    }

    private static GameSession CreateDailySession(bool isLegacyDuplicate) => new()
    {
        GameId = Guid.NewGuid(),
        PlayerId = PlayerId,
        Mode = GameMode.Daily,
        DailyNumber = 5,
        TargetCharacterId = 1,
        StartedOnUtc = DateTimeOffset.UtcNow,
        IsLegacyDuplicate = isLegacyDuplicate
    };

    private sealed class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);
    }
}
