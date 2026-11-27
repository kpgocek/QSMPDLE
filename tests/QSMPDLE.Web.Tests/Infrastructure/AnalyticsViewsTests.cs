using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace QSMPDLE.Web.Tests.Infrastructure;

/// <summary>The views compare with the database's clock, so the data is placed around the current UTC day.</summary>
public sealed class AnalyticsViewsTests : IAsyncLifetime
{
    private const string MigrationBeforeAnalytics = "20261122174856_DropLegacySessionColumns";
    private const int Answer = 100;

    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18").Build();
    private readonly DayService dayService = new();
    private DbContextOptions<ApplicationDbContext> options = null!;

    private int Today => dayService.GetTodayDayNumber();

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;
    }

    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    [Fact]
    public async Task Migrate_ExistingDataWithDuplicates_IsKeptAndLabelledUnknown()
    {
        await using (var database = new ApplicationDbContext(options))
        {
            await database.GetService<IMigrator>().MigrateAsync(MigrationBeforeAnalytics);
            await database.Database.ExecuteSqlRawAsync("""
                INSERT INTO "GameStats" ("Id", "GameId", "PlayerId", "PuzzleId", "TargetCharacterId", "StartedOnUtc", "IsWon")
                VALUES (1, gen_random_uuid(), gen_random_uuid(), 14, 100, '2026-06-28 20:00Z', FALSE);
                INSERT INTO "GameGuess" ("GameId", "GameSessionId", "GuessOrder", "GuessedCharacterId")
                SELECT s."GameId", 1, 0, 7 FROM "GameStats" s, generate_series(1, 6);
                """);
            await database.Database.MigrateAsync();
        }

        var session = (await QueryAsync("SELECT ruleset, guess_count, outcome, outcome_source, finished_at, last_guess_at FROM analytics.sessions")).Single();
        session.Should().Equal("unknown", 6, "lost", "inferred", null, null);
        (await CountAsync("SELECT sessions FROM analytics.data_quality WHERE check_name = 'repeated guess positions'")).Should().Be(1);

        await using var migrated = new ApplicationDbContext(options);
        await migrated.GetService<IMigrator>().MigrateAsync(MigrationBeforeAnalytics);
        (await CountAsync("""SELECT count(*) FROM "GameGuess" """)).Should().Be(6, "rolling back keeps the data too");
    }

    [Fact]
    public async Task Views_NoData_ReturnZerosAndNullRates()
    {
        await MigrateAsync();

        var totals = (await QueryAsync("SELECT mode, ruleset, games_started, games_completed, completion_rate_pct, win_rate_pct, avg_guesses_completed, median_guesses_completed FROM analytics.activity_totals")).Single();

        totals.Should().Equal("all", "all", 0L, 0L, null, null, null, null);
        foreach (var view in await QueryAsync("SELECT table_name FROM information_schema.views WHERE table_schema = 'analytics'"))
        {
            var query = () => QueryAsync($"SELECT * FROM analytics.{view[0]}");
            await query.Should().NotThrowAsync();
        }
    }

    [Fact]
    public async Task ActivityTotals_UnfinishedGames_CountInCompletionButNotWinRate()
    {
        await MigrateAsync();
        await SeedAsync(
            Game(EntryPoint.Practice, [Answer]),
            Game(EntryPoint.Practice, [1, 2, Answer]),
            Game(EntryPoint.Practice, [1, 2, 3, 4, 5, 6]),
            Game(EntryPoint.Practice, [1]));

        var totals = (await QueryAsync("SELECT games_started, games_completed, games_won, games_lost, games_unfinished, completion_rate_pct, win_rate_pct, avg_guesses_completed, median_guesses_completed, avg_guesses_won FROM analytics.activity_totals WHERE mode = 'all' AND ruleset = 'all'")).Single();

        totals.Should().Equal(4L, 3L, 2L, 1L, 1L, 75.0m, 66.7m, 3.33m, 3.0d, 2.00m);
    }

    [Fact]
    public async Task Sessions_Modes_FollowTheEntryPointAndThePuzzleDay()
    {
        await MigrateAsync();
        var openedDailyPlayedLater = Game(EntryPoint.Daily, [1], puzzleId: Today - 5);
        var duplicate = Game(EntryPoint.Daily, [1]);
        duplicate.IsLegacyDuplicate = true;
        await SeedAsync(Game(EntryPoint.Daily, [1]), Game(EntryPoint.Archive, [1], puzzleId: Today - 5), openedDailyPlayedLater, Game(EntryPoint.Practice, [1]), duplicate);

        var modes = await QueryAsync("SELECT mode, count(*) FROM analytics.sessions GROUP BY mode ORDER BY mode");
        var puzzleDate = await QueryAsync("SELECT DISTINCT puzzle_date FROM analytics.sessions WHERE mode = 'daily'");

        modes.Should().BeEquivalentTo(new object[][] { ["archive", 2L], ["daily", 1L], ["practice", 1L] });
        puzzleDate.Single().Single().Should().Be(dayService.GetArchiveDate(Today), "the views repeat DayService's first day");
    }

    [Fact]
    public async Task Sessions_InactiveForADay_AreAbandonedUntilPlayedAgain()
    {
        await MigrateAsync();
        var now = DateTimeOffset.UtcNow;
        var resumed = Game(EntryPoint.Practice, [1, 2], startedOnUtc: now.AddDays(-3));
        resumed.Guesses.Last().GuessedOnUtc = now.AddHours(-1);
        await SeedAsync(
            Game(EntryPoint.Practice, [1], startedOnUtc: now.AddHours(-25)),
            Game(EntryPoint.Practice, [1], startedOnUtc: now.AddHours(-23)),
            Game(EntryPoint.Practice, [Answer], startedOnUtc: now.AddDays(-3)),
            resumed);

        (await CountAsync("SELECT count(*) FROM analytics.sessions WHERE is_abandoned")).Should().Be(1);
    }

    [Fact]
    public async Task DailyPuzzles_ReportOnlyPlayedDailiesAndNeverTheFutureSchedule()
    {
        await MigrateAsync();
        await SeedCharactersAsync();
        await using (var database = new ApplicationDbContext(options))
        {
            database.DailyGames.AddRange(new[] { -2, -1, 0, 1, 2 }.Select(offset => new DailyGame { Id = Today + offset, Date = dayService.GetArchiveDate(Today + offset), CharacterId = offset > 0 ? 101 : Answer }));
            await database.SaveChangesAsync();
        }
        await SeedAsync(
            Game(EntryPoint.Daily, [Answer]),
            Game(EntryPoint.Daily, [1, Answer]),
            Game(EntryPoint.Daily, [1, 2, 3, 4, 5, 6]),
            Game(EntryPoint.Daily, [1]),
            Game(EntryPoint.Archive, [Answer], puzzleId: Today - 1));

        var puzzles = await QueryAsync("SELECT puzzle_id, answer, games_started, games_completed, games_won, games_lost, win_rate_pct, avg_guesses_won, wins_in_1, wins_in_2, difficulty_rank FROM analytics.daily_puzzles ORDER BY puzzle_id DESC");
        var answers = await QueryAsync("SELECT character, daily_puzzles FROM analytics.answer_frequency WHERE character_id >= 100");

        puzzles.Should().HaveCount(3);
        puzzles[0].Should().Equal(Today, "Character 100", 4L, 3L, 2L, 1L, 66.7m, 1.50m, 1L, 1L, null);
        puzzles[1].Take(5).Should().Equal(Today - 1, "Character 100", 0L, 0L, 0L);
        answers.Should().ContainSingle("a character released tomorrow is not listed").Which.Should().Equal("Character 100", 3L);
    }

    [Fact]
    public async Task DailyPuzzles_EnoughCompletedGames_AreRankedWithinTheirRuleset()
    {
        await MigrateAsync();
        await SeedAsync([
            .. Enumerable.Range(0, 10).Select(index => Game(EntryPoint.Daily, index < 3 ? [Answer] : [1, 2, 3, 4, 5, 6])),
            .. Enumerable.Range(0, 10).Select(_ => Game(EntryPoint.Daily, [Answer], puzzleId: Today - 1, startedOnUtc: StartOf(Today - 1))),
            .. Enumerable.Range(0, 9).Select(_ => Game(EntryPoint.Daily, [1, 2, 3, 4, 5, 6], puzzleId: Today - 2, startedOnUtc: StartOf(Today - 2)))]);

        var ranks = await QueryAsync("SELECT puzzle_id, difficulty_rank FROM analytics.daily_puzzles ORDER BY puzzle_id DESC");
        var answerRank = await QueryAsync("SELECT games_completed, difficulty_rank FROM analytics.character_answers");

        ranks.Should().BeEquivalentTo(new object?[][] { [Today, 1L], [Today - 1, 2L], [Today - 2, null] }, "nine games are too few to rank");
        answerRank.Single().Should().Equal(29L, 1L);
    }

    [Fact]
    public async Task CharacterGuesses_SeparatePopularityFromAnswers()
    {
        await MigrateAsync();
        await SeedCharactersAsync();
        await SeedAsync(
            Game(EntryPoint.Practice, [1, Answer]),
            Game(EntryPoint.Practice, [1, 2]),
            Game(EntryPoint.Practice, [2, 1]));

        var guesses = await QueryAsync("SELECT character_id, guesses, correct_guesses, first_guesses, guess_share_pct, first_guess_share_pct FROM analytics.character_guesses ORDER BY character_id");
        var answers = await QueryAsync("SELECT character_id, games_started FROM analytics.character_answers");

        guesses.Should().BeEquivalentTo(new object[][]
        {
            [1, 3L, 0L, 2L, 50.00m, 66.67m],
            [2, 2L, 0L, 1L, 33.33m, 33.33m],
            [Answer, 1L, 1L, 0L, 16.67m, 0.00m],
        });
        answers.Single().Should().Equal(Answer, 3L);
    }

    [Fact]
    public async Task VersionComparison_NeverMixesRulesets()
    {
        await MigrateAsync();
        var player = Guid.NewGuid();
        await SeedAsync(
            Game(EntryPoint.Practice, [Answer], playerId: player, ruleset: Ruleset.Classic),
            Game(EntryPoint.Practice, [1, 2, 3, 4, 5, 6], playerId: player, ruleset: Ruleset.V2),
            Game(EntryPoint.Practice, [1, 2, 3, 4, 5, 6], playerId: player, ruleset: Ruleset.V2),
            Game(EntryPoint.Practice, [1], ruleset: Ruleset.Unknown));

        var versions = await QueryAsync("SELECT ruleset, games_started, win_rate_pct, games_per_browser FROM analytics.version_comparison WHERE mode = 'all' ORDER BY ruleset");

        versions.Should().BeEquivalentTo(new object?[][] { ["2.0", 2L, 0.0m, 2.00m], ["classic", 1L, 100.0m, 1.00m], ["unknown", 1L, null, 1.00m] });
    }

    [Fact]
    public async Task Retention_ReturnDayNotOver_IsUnknownAndOtherwiseCountsBrowsers()
    {
        await MigrateAsync();
        var (stayed, left, fresh) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await SeedAsync(
            PracticeOn(stayed, -10), PracticeOn(stayed, -10), PracticeOn(stayed, -9), PracticeOn(stayed, -3),
            PracticeOn(left, -10),
            PracticeOn(fresh, -1), PracticeOn(fresh, 0));

        var cohorts = await QueryAsync("SELECT browsers, returned_d1, d1_retention_pct, returned_d7, d7_retention_pct FROM analytics.retention ORDER BY cohort_day");
        var today = await QueryAsync("SELECT active_browsers, new_browsers, returning_browsers FROM analytics.daily_active_browsers ORDER BY day DESC LIMIT 1");

        cohorts[0].Should().Equal(2L, 1L, 50.0m, 1L, 50.0m);
        cohorts[1].Should().Equal(1L, null, null, null, null);
        today.Single().Should().Equal(1L, 0L, 1L);
    }

    [Fact]
    public async Task DailyStreaks_GapStartsANewStreakAndYesterdayKeepsItCurrent()
    {
        await MigrateAsync();
        var player = Guid.NewGuid();
        await SeedAsync([.. new[] { -6, -5, -4, -2, -1 }.Select(offset => Game(EntryPoint.Daily, [1], playerId: player, puzzleId: Today + offset, startedOnUtc: StartOf(Today + offset)))]);

        var streaks = await QueryAsync("SELECT days, is_current FROM analytics.daily_streaks ORDER BY first_day");

        streaks.Should().BeEquivalentTo(new object[][] { [3L, false], [2L, true] });
    }

    [Fact]
    public async Task ReportingQueries_EveryStatement_Runs()
    {
        await MigrateAsync();
        await SeedCharactersAsync();
        await SeedAsync(Game(EntryPoint.Daily, [1, Answer]), Game(EntryPoint.Practice, [1]));
        var script = await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "docs", "analytics-queries.sql"));

        // Comments may contain semicolons, statements do not.
        var sql = string.Join('\n', script.Split('\n').Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal)));
        var statements = sql.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        statements.Should().NotBeEmpty();
        foreach (var statement in statements)
        {
            var query = () => QueryAsync(statement);
            await query.Should().NotThrowAsync(statement);
        }
    }

    private static string RepositoryRoot([CallerFilePath] string path = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", "..", ".."));

    private DateTimeOffset StartOf(int puzzleId) => new(dayService.GetArchiveDate(puzzleId).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private GameSession PracticeOn(Guid playerId, int dayOffset) => Game(EntryPoint.Practice, [1], playerId, startedOnUtc: StartOf(Today + dayOffset));

    private GameSession Game(EntryPoint entryPoint, int[] guesses, Guid? playerId = null, int? puzzleId = null, DateTimeOffset? startedOnUtc = null, Ruleset ruleset = Ruleset.Classic)
    {
        var started = startedOnUtc ?? StartOf(Today);
        var game = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = playerId ?? Guid.NewGuid(),
            PuzzleId = entryPoint == EntryPoint.Practice ? null : puzzleId ?? Today,
            SessionCategory = entryPoint == EntryPoint.Practice ? SessionCategory.Practice : SessionCategory.CanonicalPuzzle,
            FirstEntryPoint = entryPoint,
            TargetCharacterId = Answer,
            Ruleset = ruleset,
            StartedOnUtc = started,
        };

        for (var count = 1; count <= guesses.Length; count++)
            game.RecordGuesses([.. guesses.Take(count)], started);

        return game;
    }

    private async Task MigrateAsync()
    {
        await using var database = new ApplicationDbContext(options);
        await database.Database.MigrateAsync();
    }

    private async Task SeedAsync(params GameSession[] sessions)
    {
        await using var database = new ApplicationDbContext(options);
        database.GameStats.AddRange(sessions);
        await database.SaveChangesAsync();
    }

    private async Task SeedCharactersAsync()
    {
        await using var database = new ApplicationDbContext(options);
        database.Characters.AddRange(new[] { 1, 2, Answer, 101 }.Select(id => new Character
        {
            Id = id,
            Name = $"Character {id}",
            AvailableFrom = id == 101 ? dayService.GetArchiveDate(Today + 1) : null,
        }));
        await database.SaveChangesAsync();
    }

    private async Task<long> CountAsync(string sql) => (long)(await QueryAsync(sql)).Single().Single()!;

    private async Task<List<object?[]>> QueryAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        List<object?[]> rows = [];
        while (await reader.ReadAsync())
        {
            rows.Add([.. Enumerable.Range(0, reader.FieldCount).Select(index => reader.IsDBNull(index) ? null : reader.GetValue(index))]);
        }

        return rows;
    }
}
