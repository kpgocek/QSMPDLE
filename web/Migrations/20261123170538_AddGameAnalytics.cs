using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QSMPDLE.Web.Migrations
{
    /// <summary>
    /// Adds the ruleset and guess times, and the reporting views described in docs/analytics.md. Additive only:
    /// existing rows keep their values, and their ruleset and guess times stay unknown.
    /// </summary>
    public partial class AddGameAnalytics : Migration
    {
        // One definition, so a metric means the same in every view. Needs a relation "s" shaped like analytics.sessions.
        private const string Metrics = """
            count(s.session_id) AS games_started,
            count(*) FILTER (WHERE s.outcome IN ('won', 'lost')) AS games_completed,
            count(*) FILTER (WHERE s.outcome = 'won') AS games_won,
            count(*) FILTER (WHERE s.outcome = 'lost') AS games_lost,
            count(*) FILTER (WHERE s.outcome = 'unfinished') AS games_unfinished,
            count(*) FILTER (WHERE s.is_abandoned) AS games_abandoned,
            round(100.0 * count(*) FILTER (WHERE s.outcome IN ('won', 'lost')) / NULLIF(count(s.session_id), 0), 1) AS completion_rate_pct,
            round(100.0 * count(*) FILTER (WHERE s.outcome = 'won') / NULLIF(count(*) FILTER (WHERE s.outcome IN ('won', 'lost')), 0), 1) AS win_rate_pct,
            round(avg(s.guess_count) FILTER (WHERE s.outcome IN ('won', 'lost')), 2) AS avg_guesses_completed,
            percentile_cont(0.5) WITHIN GROUP (ORDER BY s.guess_count) FILTER (WHERE s.outcome IN ('won', 'lost')) AS median_guesses_completed,
            round(avg(s.guess_count) FILTER (WHERE s.outcome = 'won'), 2) AS avg_guesses_won,
            count(DISTINCT s.player_id) AS browsers
            """;

        // Games below this many completions are shown but not ranked.
        private const string MinimumSample = "10";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Ruleset",
                table: "GameStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GuessedOnUtc",
                table: "GameGuess",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameGuess_GameSessionId_GuessedCharacterId",
                table: "GameGuess",
                columns: new[] { "GameSessionId", "GuessedCharacterId" },
                unique: true,
                filter: "\"GuessedOnUtc\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GameGuess_GameSessionId_GuessOrder",
                table: "GameGuess",
                columns: new[] { "GameSessionId", "GuessOrder" },
                unique: true,
                filter: "\"GuessedOnUtc\" IS NOT NULL");

            // EF cannot model an expression index. Measured on 200k games: date-filtered activity uses it instead of a full scan.
            migrationBuilder.Sql("""
                CREATE INDEX "IX_GameStats_StartedDate" ON "GameStats" ((("StartedOnUtc" AT TIME ZONE 'UTC')::date));
                """);

            CreateViews(migrationBuilder);
        }

        private static void CreateViews(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE SCHEMA analytics;");

            // LATERAL keeps the guess lookup on its index when only some sessions are wanted.
            migrationBuilder.Sql("""
                CREATE VIEW analytics.sessions AS
                SELECT
                    s."Id" AS session_id,
                    s."PlayerId" AS player_id,
                    CASE
                        WHEN s."SessionCategory" = 1 THEN 'practice'
                        WHEN s."PuzzleId" IS NULL THEN 'unknown'
                        WHEN s."FirstEntryPoint" = 0 AND (s."StartedOnUtc" AT TIME ZONE 'UTC')::date = DATE '2026-06-15' + s."PuzzleId" - 1 THEN 'daily'
                        ELSE 'archive'
                    END AS mode,
                    CASE s."Ruleset" WHEN 1 THEN 'classic' WHEN 2 THEN '2.0' ELSE 'unknown' END AS ruleset,
                    s."PuzzleId" AS puzzle_id,
                    DATE '2026-06-15' + s."PuzzleId" - 1 AS puzzle_date,
                    s."TargetCharacterId" AS answer_character_id,
                    s."StartedOnUtc" AS started_at,
                    (s."StartedOnUtc" AT TIME ZONE 'UTC')::date AS started_date,
                    s."FinishedOnUtc" AS finished_at,
                    g.guess_count,
                    g.last_guess_at,
                    o.outcome,
                    CASE WHEN s."FinishedOnUtc" IS NOT NULL THEN 'recorded' WHEN o.outcome <> 'unfinished' THEN 'inferred' END AS outcome_source,
                    o.outcome = 'unfinished' AND COALESCE(g.last_guess_at, s."StartedOnUtc") < now() - INTERVAL '24 hours' AS is_abandoned
                FROM "GameStats" s
                CROSS JOIN LATERAL (
                    SELECT count(*)::int AS guess_count,
                           COALESCE(bool_or(guess."GuessedCharacterId" = s."TargetCharacterId"), FALSE) AS has_answer,
                           max(guess."GuessedOnUtc") AS last_guess_at
                    FROM "GameGuess" guess
                    WHERE guess."GameSessionId" = s."Id") g
                CROSS JOIN LATERAL (
                    SELECT CASE
                        WHEN s."FinishedOnUtc" IS NOT NULL THEN CASE WHEN s."IsWon" THEN 'won' ELSE 'lost' END
                        WHEN g.has_answer THEN 'won'
                        WHEN g.guess_count >= 6 AND s."TargetCharacterId" <> 0 THEN 'lost'
                        ELSE 'unfinished'
                    END AS outcome) o
                WHERE NOT s."IsLegacyDuplicate";
                """);

            migrationBuilder.Sql("""
                CREATE VIEW analytics.guesses AS
                SELECT
                    s.session_id,
                    s.player_id,
                    s.mode,
                    s.ruleset,
                    s.puzzle_id,
                    s.puzzle_date,
                    s.started_date,
                    g."GuessOrder" + 1 AS guess_number,
                    g."GuessedCharacterId" AS character_id,
                    g."GuessedCharacterId" = s.answer_character_id AS is_correct,
                    g."GuessedOnUtc" AS guessed_at
                FROM analytics.sessions s
                JOIN "GameGuess" g ON g."GameSessionId" = s.session_id;
                """);

            foreach (var (period, start) in new[] { ("day", "s.started_date"), ("week", "date_trunc('week', s.started_date)::date"), ("month", "date_trunc('month', s.started_date)::date") })
            {
                // The VALUES lists add explicit 'all' rows, which medians and distinct counts cannot be summed into.
                migrationBuilder.Sql($"""
                    CREATE VIEW analytics.activity_by_{period} AS
                    SELECT {start} AS {period}, m.mode, r.ruleset, {Metrics}
                    FROM analytics.sessions s
                    CROSS JOIN LATERAL (VALUES (s.mode), ('all')) m(mode)
                    CROSS JOIN LATERAL (VALUES (s.ruleset), ('all')) r(ruleset)
                    GROUP BY 1, 2, 3;
                    """);
            }

            migrationBuilder.Sql($"""
                CREATE VIEW analytics.activity_totals AS
                SELECT COALESCE(s.mode, 'all') AS mode, COALESCE(s.ruleset, 'all') AS ruleset, {Metrics}
                FROM analytics.sessions s
                GROUP BY GROUPING SETS ((), (s.mode), (s.ruleset), (s.mode, s.ruleset));
                """);

            migrationBuilder.Sql($"""
                CREATE VIEW analytics.version_comparison AS
                SELECT s.ruleset,
                    CASE s.ruleset
                        WHEN 'classic' THEN 'Five clues and the pre-2.0 roster'
                        WHEN '2.0' THEN 'Seven clues and the enlarged roster; a harder search with more information'
                        ELSE 'Played before rulesets were recorded; clues, roster and character data varied'
                    END AS rules,
                    m.mode, {Metrics},
                    round(count(*)::numeric / count(DISTINCT s.player_id), 2) AS games_per_browser,
                    count(DISTINCT s.started_date) AS active_days,
                    min(s.started_date) AS first_day,
                    max(s.started_date) AS last_day
                FROM analytics.sessions s
                CROSS JOIN LATERAL (VALUES (s.mode), ('all')) m(mode)
                GROUP BY s.ruleset, m.mode;
                """);

            // Days already played, plus past days nobody played; the schedule beyond today is never read.
            migrationBuilder.Sql($"""
                CREATE VIEW analytics.daily_puzzles AS
                WITH puzzles AS (
                    SELECT d.id AS puzzle_id, d.date AS puzzle_date, d.character_id AS answer_character_id
                    FROM "DailyGames" d
                    WHERE d.date <= (now() AT TIME ZONE 'UTC')::date
                    UNION
                    SELECT s.puzzle_id, s.puzzle_date, s.answer_character_id
                    FROM analytics.sessions s
                    WHERE s.mode = 'daily'),
                results AS (
                    SELECT p.puzzle_id, p.puzzle_date, p.answer_character_id,
                        string_agg(DISTINCT s.ruleset, ' + ') AS rulesets, {Metrics},
                        count(*) FILTER (WHERE s.outcome = 'won' AND s.guess_count = 1) AS wins_in_1,
                        count(*) FILTER (WHERE s.outcome = 'won' AND s.guess_count = 2) AS wins_in_2,
                        count(*) FILTER (WHERE s.outcome = 'won' AND s.guess_count = 3) AS wins_in_3,
                        count(*) FILTER (WHERE s.outcome = 'won' AND s.guess_count = 4) AS wins_in_4,
                        count(*) FILTER (WHERE s.outcome = 'won' AND s.guess_count = 5) AS wins_in_5,
                        count(*) FILTER (WHERE s.outcome = 'won' AND s.guess_count = 6) AS wins_in_6
                    FROM puzzles p
                    LEFT JOIN analytics.sessions s
                        ON s.mode = 'daily' AND s.puzzle_id = p.puzzle_id AND s.answer_character_id = p.answer_character_id
                    GROUP BY p.puzzle_id, p.puzzle_date, p.answer_character_id)
                SELECT results.*, c.name AS answer,
                    CASE WHEN games_completed >= {MinimumSample} THEN rank() OVER (
                        PARTITION BY rulesets, games_completed >= {MinimumSample}
                        ORDER BY win_rate_pct, avg_guesses_won DESC NULLS FIRST) END AS difficulty_rank
                FROM results
                LEFT JOIN "Characters" c ON c.id = results.answer_character_id;
                """);

            migrationBuilder.Sql("""
                CREATE VIEW analytics.character_guesses AS
                SELECT g.character_id, c.name AS character, g.ruleset, g.mode,
                    count(*) AS guesses,
                    count(*) FILTER (WHERE g.is_correct) AS correct_guesses,
                    count(*) FILTER (WHERE g.guess_number = 1) AS first_guesses,
                    round(100.0 * count(*) / sum(count(*)) OVER (PARTITION BY g.ruleset, g.mode), 2) AS guess_share_pct,
                    round(100.0 * count(*) FILTER (WHERE g.guess_number = 1)
                        / NULLIF(sum(count(*) FILTER (WHERE g.guess_number = 1)) OVER (PARTITION BY g.ruleset, g.mode), 0), 2) AS first_guess_share_pct
                FROM analytics.guesses g
                LEFT JOIN "Characters" c ON c.id = g.character_id
                GROUP BY g.character_id, c.name, g.ruleset, g.mode;
                """);

            migrationBuilder.Sql($"""
                CREATE VIEW analytics.character_answers AS
                WITH results AS (
                    SELECT s.answer_character_id AS character_id, s.ruleset, s.mode, {Metrics}
                    FROM analytics.sessions s
                    WHERE s.answer_character_id <> 0
                    GROUP BY s.answer_character_id, s.ruleset, s.mode)
                SELECT results.*, c.name AS character,
                    CASE WHEN games_completed >= {MinimumSample} THEN rank() OVER (
                        PARTITION BY ruleset, mode, games_completed >= {MinimumSample}
                        ORDER BY win_rate_pct, avg_guesses_won DESC NULLS FIRST) END AS difficulty_rank
                FROM results
                LEFT JOIN "Characters" c ON c.id = results.character_id;
                """);

            migrationBuilder.Sql("""
                CREATE VIEW analytics.answer_frequency AS
                SELECT c.id AS character_id, c.name AS character,
                    (SELECT count(*) FROM "DailyGames" d WHERE d.character_id = c.id AND d.date <= (now() AT TIME ZONE 'UTC')::date) AS daily_puzzles,
                    (SELECT max(d.date) FROM "DailyGames" d WHERE d.character_id = c.id AND d.date <= (now() AT TIME ZONE 'UTC')::date) AS last_daily_puzzle,
                    (SELECT count(*) FROM "GameStats" s WHERE s."TargetCharacterId" = c.id AND s."SessionCategory" = 1 AND NOT s."IsLegacyDuplicate") AS practice_games
                FROM "Characters" c
                WHERE c.available_from IS NULL OR c.available_from <= (now() AT TIME ZONE 'UTC')::date;
                """);

            migrationBuilder.Sql("""
                CREATE VIEW analytics.daily_active_browsers AS
                WITH days AS (
                    SELECT s.player_id, s.started_date AS day, bool_or(s.mode = 'daily') AS played_daily
                    FROM analytics.sessions s
                    GROUP BY s.player_id, s.started_date),
                firsts AS (
                    SELECT d.*, min(d.day) OVER (PARTITION BY d.player_id) AS first_day
                    FROM days d)
                SELECT day,
                    count(*) AS active_browsers,
                    count(*) FILTER (WHERE day = first_day) AS new_browsers,
                    count(*) FILTER (WHERE day > first_day) AS returning_browsers,
                    count(*) FILTER (WHERE played_daily) AS daily_puzzle_browsers
                FROM firsts
                GROUP BY day;
                """);

            // A rate stays null until its whole return day has passed.
            migrationBuilder.Sql("""
                CREATE VIEW analytics.retention AS
                WITH days AS (
                    SELECT DISTINCT s.player_id, s.started_date AS day
                    FROM analytics.sessions s),
                firsts AS (
                    SELECT player_id, min(day) AS cohort_day
                    FROM days
                    GROUP BY player_id),
                cohorts AS (
                    SELECT f.cohort_day,
                        count(DISTINCT f.player_id) AS browsers,
                        count(*) FILTER (WHERE d.day = f.cohort_day + 1) AS returned_d1,
                        count(*) FILTER (WHERE d.day = f.cohort_day + 7) AS returned_d7
                    FROM firsts f
                    JOIN days d USING (player_id)
                    GROUP BY f.cohort_day)
                SELECT cohort_day, browsers,
                    CASE WHEN d1_known THEN returned_d1 END AS returned_d1,
                    CASE WHEN d1_known THEN round(100.0 * returned_d1 / browsers, 1) END AS d1_retention_pct,
                    CASE WHEN d7_known THEN returned_d7 END AS returned_d7,
                    CASE WHEN d7_known THEN round(100.0 * returned_d7 / browsers, 1) END AS d7_retention_pct
                FROM cohorts
                CROSS JOIN LATERAL (
                    SELECT cohort_day + 1 < (now() AT TIME ZONE 'UTC')::date AS d1_known,
                           cohort_day + 7 < (now() AT TIME ZONE 'UTC')::date AS d7_known) known;
                """);

            migrationBuilder.Sql("""
                CREATE VIEW analytics.daily_streaks AS
                WITH days AS (
                    SELECT DISTINCT s.player_id, s.puzzle_date AS day
                    FROM analytics.sessions s
                    WHERE s.mode = 'daily'),
                numbered AS (
                    SELECT d.*, d.day - (row_number() OVER (PARTITION BY d.player_id ORDER BY d.day))::int AS island
                    FROM days d)
                SELECT player_id, min(day) AS first_day, max(day) AS last_day, count(*) AS days,
                    max(day) >= (now() AT TIME ZONE 'UTC')::date - 1 AS is_current
                FROM numbered
                GROUP BY player_id, island;
                """);

            migrationBuilder.Sql("""
                CREATE VIEW analytics.data_quality AS
                WITH guesses AS (
                    SELECT g."GameSessionId" AS session_id,
                        count(*) AS total,
                        count(DISTINCT g."GuessOrder") AS orders,
                        count(DISTINCT g."GuessedCharacterId") AS characters,
                        max(g."GuessOrder") - min(g."GuessOrder") + 1 AS span
                    FROM "GameGuess" g
                    GROUP BY g."GameSessionId")
                SELECT checks.check_name, checks.sessions
                FROM (
                    SELECT count(*) AS total,
                        count(*) FILTER (WHERE s."IsLegacyDuplicate") AS legacy_duplicates,
                        count(*) FILTER (WHERE s."Ruleset" = 0) AS ruleset_unknown,
                        count(*) FILTER (WHERE a.outcome_source = 'inferred') AS outcome_inferred,
                        count(*) FILTER (WHERE s."TargetCharacterId" = 0) AS answer_missing,
                        count(*) FILTER (WHERE a.mode = 'unknown') AS mode_unknown,
                        count(*) FILTER (WHERE g.total IS NULL) AS no_guesses,
                        count(*) FILTER (WHERE g.total > 6) AS too_many_guesses,
                        count(*) FILTER (WHERE g.orders < g.total) AS repeated_positions,
                        count(*) FILTER (WHERE g.characters < g.total) AS repeated_characters,
                        count(*) FILTER (WHERE g.span > g.total) AS missing_positions,
                        count(*) FILTER (WHERE s."FinishedOnUtc" < s."StartedOnUtc") AS finished_before_start
                    FROM "GameStats" s
                    LEFT JOIN analytics.sessions a ON a.session_id = s."Id"
                    LEFT JOIN guesses g ON g.session_id = s."Id") totals
                CROSS JOIN LATERAL (VALUES
                    ('total sessions', total),
                    ('excluded: legacy duplicate', legacy_duplicates),
                    ('ruleset unknown', ruleset_unknown),
                    ('outcome inferred from guesses', outcome_inferred),
                    ('answer missing', answer_missing),
                    ('mode unknown', mode_unknown),
                    ('no guesses', no_guesses),
                    ('more than 6 guesses', too_many_guesses),
                    ('repeated guess positions', repeated_positions),
                    ('repeated characters', repeated_characters),
                    ('missing guess positions', missing_positions),
                    ('finished before started', finished_before_start)) checks(check_name, sessions);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS analytics CASCADE;");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_GameStats_StartedDate";""");

            migrationBuilder.DropIndex(
                name: "IX_GameGuess_GameSessionId_GuessedCharacterId",
                table: "GameGuess");

            migrationBuilder.DropIndex(
                name: "IX_GameGuess_GameSessionId_GuessOrder",
                table: "GameGuess");

            migrationBuilder.DropColumn(
                name: "Ruleset",
                table: "GameStats");

            migrationBuilder.DropColumn(
                name: "GuessedOnUtc",
                table: "GameGuess");
        }
    }
}
