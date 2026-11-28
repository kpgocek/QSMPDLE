using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace QSMPDLE.Web.Tests.Infrastructure;

/// <summary>Covers sessions that an older version wrote without the canonical columns.</summary>
public sealed class LegacySessionColumnsMigrationTests : IAsyncLifetime
{
    private const string MigrationBefore = "20261122120813_AddCharacterAvailableFrom";

    private static readonly Guid WithCanonicalGame = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid WithTwoMissedGames = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid WithMissedArchive = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid WithMissedPractice = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18").Build();
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;

        await using var database = new ApplicationDbContext(options);
        await database.GetService<IMigrator>().MigrateAsync(MigrationBefore);
        await database.Database.ExecuteSqlAsync($"""
                                                 INSERT INTO "GameStats" ("GameId", "PlayerId", "PuzzleId", "DailyNumber", "Mode", "TargetCharacterId", "StartedOnUtc", "FinishedOnUtc", "IsWon")
                                                 VALUES (gen_random_uuid(), {WithCanonicalGame}, 5, 5, 0, 1, now(), NULL, false),
                                                        (gen_random_uuid(), {WithCanonicalGame}, NULL, 5, 0, 1, now() - interval '1 hour', now(), true),
                                                        (gen_random_uuid(), {WithTwoMissedGames}, NULL, 5, 1, 1, now() - interval '2 hours', now(), false),
                                                        (gen_random_uuid(), {WithTwoMissedGames}, NULL, 5, 0, 1, now() - interval '1 hour', now(), true),
                                                        (gen_random_uuid(), {WithMissedArchive}, NULL, 5, 1, 1, now(), now(), true),
                                                        (gen_random_uuid(), {WithMissedPractice}, NULL, NULL, 2, 1, now(), now(), true);
                                                 """);
        await database.Database.MigrateAsync();
    }

    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    [Fact]
    public async Task Migrate_MissedRowOfAPuzzleWithACanonicalSession_BecomesLegacyDuplicate()
    {
        var sessions = await GetSessionsAsync(WithCanonicalGame);

        sessions.Should().HaveCount(2).And.OnlyContain(session => session.PuzzleId == 5);
        sessions.Should().ContainSingle(session => !session.IsLegacyDuplicate).Which.FinishedOnUtc.Should()
            .BeNull("the session the player has been seeing stays");
    }

    [Fact]
    public async Task Migrate_TwoMissedRowsOfOnePuzzle_KeepsTheFinishedDaily()
    {
        var sessions = await GetSessionsAsync(WithTwoMissedGames);

        sessions.Should().HaveCount(2).And.OnlyContain(session => session.PuzzleId == 5);
        sessions.Should().ContainSingle(session => !session.IsLegacyDuplicate).Which.FirstEntryPoint.Should()
            .Be(EntryPoint.Daily);
    }

    [Fact]
    public async Task Migrate_MissedArchiveRow_KeepsItsEntryPoint()
    {
        var session = (await GetSessionsAsync(WithMissedArchive)).Single();

        session.PuzzleId.Should().Be(5);
        session.FirstEntryPoint.Should().Be(EntryPoint.Archive);
        session.SessionCategory.Should().Be(SessionCategory.CanonicalPuzzle);
        session.IsLegacyDuplicate.Should().BeFalse();
    }

    [Fact]
    public async Task Migrate_MissedPracticeRow_StaysPractice()
    {
        var session = (await GetSessionsAsync(WithMissedPractice)).Single();

        session.PuzzleId.Should().BeNull();
        session.FirstEntryPoint.Should().Be(EntryPoint.Practice);
        session.SessionCategory.Should().Be(SessionCategory.Practice);
    }

    [Fact]
    public async Task Migrate_VersionStillRunningDuringTheRelease_CanSaveGames()
    {
        await using var database = new ApplicationDbContext(options);
        var player = Guid.NewGuid();

        await database.Database.ExecuteSqlAsync($"""
                                                 INSERT INTO "GameStats" ("GameId", "PlayerId", "PuzzleId", "DailyNumber", "Mode", "SessionCategory", "FirstEntryPoint", "IsLegacyDuplicate", "TargetCharacterId", "StartedOnUtc", "IsWon")
                                                 VALUES (gen_random_uuid(), {player}, 6, 6, 1, 0, 1, false, 1, now(), false);
                                                 """);
        database.GameStats.Add(new GameSession
        {
            GameId = Guid.NewGuid(), PlayerId = player, PuzzleId = 7, TargetCharacterId = 1
        });
        await database.SaveChangesAsync();

        (await GetSessionsAsync(player)).Should()
            .HaveCount(2, "both versions write to the table until the old one stops");
    }

    private async Task<List<GameSession>> GetSessionsAsync(Guid playerId)
    {
        await using var database = new ApplicationDbContext(options);
        return await database.GameStats.AsNoTracking().Where(session => session.PlayerId == playerId).ToListAsync();
    }
}
