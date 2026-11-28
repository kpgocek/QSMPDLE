using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QSMPDLE.Web.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace QSMPDLE.Web.Tests.Infrastructure;

public sealed class MissingAnswersMigrationTests : IAsyncLifetime
{
    private const string MigrationBefore = "20261123170538_AddGameAnalytics";

    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18").Build();
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;
    }

    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    [Fact]
    public async Task Migrate_MissingAnswers_ComeFromTheScheduleUnlessTheResultContradictsIt()
    {
        await using var database = new ApplicationDbContext(options);
        await database.GetService<IMigrator>().MigrateAsync(MigrationBefore);
        // Puzzle 1 is scheduled with character 1. Session ids: 1 open, 2 won, 3 won without that guess,
        // 4 an unscheduled puzzle, 5 practice, 6 an answer that is already known.
        await database.Database.ExecuteSqlAsync($"""
                                                 INSERT INTO "Characters" (id, name, aliases, pronouns, language_count, affiliations, species, portrait_id, wiki_slug)
                                                 SELECT id, 'Character ' || id, ARRAY[]::text[], ARRAY[]::text[], 1, ARRAY[]::text[], ARRAY['Human'], gen_random_uuid(), '' FROM generate_series(1, 2) id;
                                                 INSERT INTO "DailyGames" (id, date, character_id) VALUES (1, DATE '2026-06-15', 1);
                                                 INSERT INTO "GameStats" ("Id", "GameId", "PlayerId", "PuzzleId", "SessionCategory", "FirstEntryPoint", "TargetCharacterId", "StartedOnUtc", "FinishedOnUtc", "IsWon")
                                                 SELECT row.id, gen_random_uuid(), gen_random_uuid(), row.puzzle, row.category, 0, row.target, now(), CASE WHEN row.finished THEN now() END, row.won
                                                 FROM (VALUES (1, 1, 0, 0, false, false), (2, 1, 0, 0, true, true), (3, 1, 0, 0, true, true), (4, 9, 0, 0, false, false), (5, NULL, 1, 0, false, false), (6, 1, 0, 2, false, false))
                                                     AS row (id, puzzle, category, target, finished, won);
                                                 INSERT INTO "GameGuess" ("GameId", "GameSessionId", "GuessOrder", "GuessedCharacterId")
                                                 SELECT "GameId", "Id", 0, CASE WHEN "Id" = 2 THEN 1 ELSE 2 END FROM "GameStats" WHERE "Id" IN (2, 3);
                                                 """);

        await database.Database.MigrateAsync();

        var answers = await database.GameStats.AsNoTracking().OrderBy(session => session.Id)
            .Select(session => session.TargetCharacterId).ToListAsync();
        answers.Should().Equal(1, 1, 0, 0, 0, 2);
    }
}
