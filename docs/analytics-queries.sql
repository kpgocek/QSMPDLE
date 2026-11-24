-- QSMPDLE analytics queries for DBeaver. Definitions: docs/analytics.md.
-- Everything is UTC and read-only. A test runs every statement here against the migrated schema.
-- Rows with mode or ruleset 'all' combine every value; filter for them, never sum them with the others.

-- 1a. Today's activity
SELECT *
FROM analytics.activity_by_day
WHERE day = (now() AT TIME ZONE 'UTC')::date
ORDER BY mode, ruleset;

-- 1b. The last 30 days, including days without games
SELECT d.day::date AS day,
       COALESCE(a.games_started, 0) AS games_started,
       COALESCE(a.games_completed, 0) AS games_completed,
       a.completion_rate_pct,
       a.win_rate_pct,
       a.avg_guesses_completed,
       COALESCE(a.browsers, 0) AS browsers
FROM generate_series((now() AT TIME ZONE 'UTC')::date - 29, (now() AT TIME ZONE 'UTC')::date, INTERVAL '1 day') d(day)
LEFT JOIN (
    SELECT *
    FROM analytics.activity_by_day
    WHERE mode = 'all' AND ruleset = 'all' AND day >= (now() AT TIME ZONE 'UTC')::date - 29) a ON a.day = d.day::date
ORDER BY d.day DESC;

-- 1c. All time, by mode and ruleset
SELECT *
FROM analytics.activity_totals
ORDER BY mode, ruleset;

-- 1d. Weeks (starting Monday) and months
SELECT * FROM analytics.activity_by_week WHERE ruleset = 'all' ORDER BY week DESC, mode;
SELECT * FROM analytics.activity_by_month WHERE ruleset = 'all' ORDER BY month DESC, mode;

-- 2. Daily puzzle completion and win rates
SELECT puzzle_date, puzzle_id, answer, rulesets, games_started, games_completed, games_won, games_lost,
       completion_rate_pct, win_rate_pct, avg_guesses_won,
       wins_in_1, wins_in_2, wins_in_3, wins_in_4, wins_in_5, wins_in_6
FROM analytics.daily_puzzles
ORDER BY puzzle_date DESC;

-- 3. Hardest Daily puzzles per ruleset (at least 10 completed games); easiest: ORDER BY difficulty_rank DESC
SELECT rulesets, difficulty_rank, puzzle_date, answer, games_completed, win_rate_pct, avg_guesses_won
FROM analytics.daily_puzzles
WHERE difficulty_rank IS NOT NULL
ORDER BY rulesets, difficulty_rank
LIMIT 20;

-- 4a. Most guessed characters (popularity, not difficulty)
SELECT ruleset, mode, character, guesses, guess_share_pct, first_guesses, first_guess_share_pct, correct_guesses
FROM analytics.character_guesses
ORDER BY guesses DESC
LIMIT 30;

-- 4b. Hardest answers (at least 10 completed games)
SELECT ruleset, mode, difficulty_rank, character, games_completed, win_rate_pct, avg_guesses_completed, avg_guesses_won
FROM analytics.character_answers
WHERE difficulty_rank IS NOT NULL
ORDER BY ruleset, mode, difficulty_rank;

-- 4c. How often each released character has been an answer
SELECT *
FROM analytics.answer_frequency
ORDER BY daily_puzzles DESC, character;

-- 5. Version comparison. The rules differ (see the rules column), so a gap is not proof of what caused it.
SELECT *
FROM analytics.version_comparison
ORDER BY mode, ruleset;

-- 6a. Daily active browsers, new and returning
SELECT *
FROM analytics.daily_active_browsers
ORDER BY day DESC
LIMIT 60;

-- 6b. Retention by first day
SELECT *
FROM analytics.retention
ORDER BY cohort_day DESC
LIMIT 60;

-- 6c. Retention over all finished cohorts, weighted by size
SELECT sum(returned_d1) AS returned_d1,
       round(100.0 * sum(returned_d1) / NULLIF(sum(browsers) FILTER (WHERE d1_retention_pct IS NOT NULL), 0), 1) AS d1_retention_pct,
       sum(returned_d7) AS returned_d7,
       round(100.0 * sum(returned_d7) / NULLIF(sum(browsers) FILTER (WHERE d7_retention_pct IS NOT NULL), 0), 1) AS d7_retention_pct
FROM analytics.retention;

-- 6d. How many browsers reached each longest Daily streak (playing, not winning)
SELECT longest AS streak_days, count(*) AS browsers, count(*) FILTER (WHERE still_running) AS on_a_current_streak
FROM (
    SELECT player_id, max(days) AS longest, bool_or(is_current) AS still_running
    FROM analytics.daily_streaks
    GROUP BY player_id) streaks
GROUP BY longest
ORDER BY longest DESC;

-- 7a. Table and index sizes
SELECT c.relname AS table_name,
       pg_size_pretty(pg_table_size(c.oid)) AS table_size,
       pg_size_pretty(pg_indexes_size(c.oid)) AS index_size,
       pg_size_pretty(pg_total_relation_size(c.oid)) AS total_size,
       c.reltuples::bigint AS approximate_rows
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public' AND c.relkind = 'r'
ORDER BY pg_total_relation_size(c.oid) DESC;

-- 7b. Growth per month, and the whole database
SELECT date_trunc('month', started_date)::date AS month, count(*) AS sessions, sum(guess_count) AS guesses
FROM analytics.sessions
GROUP BY 1
ORDER BY 1 DESC;
SELECT pg_size_pretty(pg_database_size(current_database())) AS database_size;

-- 8. Recorded data that looks wrong
SELECT *
FROM analytics.data_quality;
