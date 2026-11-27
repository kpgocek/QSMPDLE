# Game analytics

Game analytics are PostgreSQL views in the `analytics` schema, read through DBeaver or any SQL client.
There is no statistics page, API, or extra service. Ready-made queries: [analytics-queries.sql](analytics-queries.sql).

Infrastructure monitoring (logs, errors, CPU, request times) is Railway's job and website traffic is Cloudflare's.
Neither is duplicated here.

## What is collected

| Table | Data |
|---|---|
| `GameStats` | One game: random game id, player id, puzzle number, category, first entry point, answer, start, finish, win, `Ruleset` |
| `GameGuess` | One guess: game, position (`GuessOrder`, from 0), character, `GuessedOnUtc` |

Everything else (correctness, outcome, guess counts, retention) is derived. All times are UTC, and a puzzle's day is
its UTC date, as in the game itself.

Recording happens once per accepted guess, in one transaction that locks the game's row
(`DatabaseGameStatsStore.RecordGuessAsync`). The rules are in `GameSession.RecordGuesses`:

| Situation | Result |
|---|---|
| Page load, refresh, reconnect, mode switch | Nothing is recorded |
| First accepted guess | The game is created, with its start time and ruleset |
| The same guess again (retry, double submit) | Counted once |
| A guess from a stale tab, or after the game ended | Not recorded; the tab shows the recorded board |
| Daily puzzle continued in Archive, or the reverse | The same game |
| Finished Archive puzzle opened again | A replay; nothing is recorded |
| Each "New Character" in Practice | A separate game, once guessed in |
| Database unreachable, Daily/Archive | The guess is refused and can be retried; logged as an error |
| Database unreachable, Practice | Play continues; logged as an error; the next guess records the missing one without a time |

## Definitions

| Term | Meaning |
|---|---|
| Started | At least one accepted guess |
| Completed | Won or lost |
| Won / lost | The answer was guessed / six guesses without it |
| Abandoned | Unfinished with no guess for 24 hours. Derived, so it ends if the player returns |
| Completion rate | Completed ÷ started |
| Win rate | Won ÷ completed. Unfinished games are not in it |
| Average/median guesses | Over completed games; a loss counts as 6. `avg_guesses_won` covers wins only |
| Mode `daily` | First opened through Daily **and** started on the puzzle's own UTC date. Anything else with a puzzle is `archive` |
| Mode `unknown` | A recorded game without a puzzle that is not Practice (old data) |
| `outcome_source` | `recorded`, or `inferred` from the guesses when an old finish was never saved |
| Browser | One player id. Not a person: a second device or cleared storage is a new browser |
| D1 / D7 retention | Share of browsers that also started a game exactly 1 / 7 days after their first day. Empty until that day is over |
| Daily streak | Consecutive days with a started Daily. The streak shown in the game counts wins instead |
| Difficulty rank | 1 is hardest: lowest win rate, then most guesses per win. Only with at least 10 completed games |

### Rulesets

`Ruleset` is set when the game is created and never changes. It comes from the clues active on the game's date
(`GameplayAvailabilityService`): the puzzle's date, or today for Practice. It follows `Gameplay:CluePremieres`, not the
deployed version. An Archive puzzle from before 2026-12-01 is `classic` even when played later.

| Value | View label | Rules |
|---|---|---|
| 0 | `unknown` | Recorded before rulesets were. 1.x rules; exact clues, roster, and character data varied |
| 1 | `classic` | Five clues, pre-2.0 roster |
| 2 | `2.0` | From the first released clue group on |

Compare versions with `analytics.version_comparison`. The rules, the roster, and the audience all differ, so a gap in
win rate doesn't show which of them caused it.

## Views

| View | One row per |
|---|---|
| `sessions`, `guesses` | Game / guess. Legacy duplicates are left out. Build new reports on these |
| `activity_by_day`, `_by_week`, `_by_month` | Period, mode, ruleset |
| `activity_totals` | Mode, ruleset |
| `version_comparison` | Ruleset, mode; with the rules spelled out |
| `daily_puzzles` | Daily puzzle up to today, including unplayed ones |
| `character_guesses` | Guessed character, ruleset, mode: **popularity** |
| `character_answers` | Answer character, ruleset, mode: **difficulty** |
| `answer_frequency` | Released character: how often it was the answer |
| `daily_active_browsers`, `retention` | Day / first day |
| `daily_streaks` | Browser and run of consecutive Dailies |
| `data_quality` | Check; counts of rows that look wrong |

`all` in `mode` or `ruleset` combines every value and is always explicit. Medians and browser counts can't be summed,
so use the `all` row.

No view is reachable from the website. They still never read the Daily schedule beyond today or list a character
before its release date.

### Changing a view

Views are created in migrations. PostgreSQL refuses to change a column a view uses, so such a migration must drop the
affected views first and recreate them. `analytics.sessions` repeats the first puzzle day (2026-06-15) and the six-guess
limit; a test compares the former with `DayService`.

## Historical data

Nothing was rewritten or deleted. Games from before this change have ruleset `unknown` and no guess times.

- Starts were always recorded at the first guess, so they are comparable.
- From 2026-06-28 to 06-29 a release only finished games after more than six guesses, so losses were never saved. The
  views show those as `lost`/`inferred`.
- Before this change the start, guess, and finish were separate writes. A few games may miss a guess or a finish, and
  simultaneous tabs may have repeated positions or more than six guesses. See `data_quality`.
- Games played before about 2026-06-26 were never recorded, so early browsers look newer than they are.
- Character data (for example affiliations) was edited over time and is not versioned. Difficulty from before an edit
  isn't strictly comparable.
- Legacy Daily/Archive duplicates stay in `GameStats` with `IsLegacyDuplicate` and are excluded from every view.

## Privacy

- The player id is random and lives in local storage. It is needed to restore games and streaks. It also links one
  browser's games, so the data is **pseudonymous, not anonymous**, and is treated as personal data.
- No IP address, location, device, account, or fingerprint is stored. Logs contain no player ids, game ids, or guesses.
- No new identifier was added. Analytics reuse the id the game already needs, which the Privacy page discloses.
  Under ePrivacy, storage that is strictly necessary for the service needs no consent; under GDPR the analysis still
  needs a legal basis (such as legitimate interest) and disclosure. This is not legal advice: check it if the audience
  or the purpose grows.
- Data is kept indefinitely. Deleting one browser's data needs its player id, which only that browser knows
  (local storage key `qsmpdle-player-stats`). Guesses go with their games:

  ```sql
  DELETE FROM "GameStats" WHERE "PlayerId" = '<player id>';
  DELETE FROM "HiLoScores" WHERE "PlayerId" = '<player id>';
  ```

  Deleting also lets that browser play the puzzles again, and removes its games from the totals.
- To keep totals but unlink old games, a retention job could later replace old player ids with random ones. Not built,
  because nothing needs it yet.

## Performance and growth

Measured locally on 200,000 games and 800,000 guesses: every view answered in 0.1 to 2.1 seconds, and a 30-day activity
query in 0.4 seconds using `IX_GameStats_StartedDate`. So there are no materialized views, caches, or jobs. Reconsider
when a query you use often takes more than a few seconds.

A game with four guesses takes about 1 kB including indexes, so 100,000 games are about 100 MB. The production volume is
1 GB. Section 7 of the queries file reports actual sizes.

## Local development and tests

The migration runs when the app starts. Connect DBeaver to `localhost:5432/qsmpdle`.

```bash
dotnet test QSMPDLE.sln                          # needs Docker
dotnet test --filter AnalyticsViews              # views, and every statement in analytics-queries.sql
```

## Deploying to Railway

1. Back up the database.
2. Optional, before deploying: check the old data (read-only).

   ```sql
   SELECT count(*) AS games,
          count(*) FILTER (WHERE "FinishedOnUtc" IS NULL) AS unfinished,
          count(*) FILTER (WHERE "IsLegacyDuplicate") AS duplicates,
          count(*) FILTER (WHERE "TargetCharacterId" = 0) AS without_answer,
          count(*) FILTER (WHERE "PuzzleId" IS NULL AND "SessionCategory" = 0) AS without_puzzle
   FROM "GameStats";

   -- Losses from 2026-06-28/29 that were never saved show up here
   SELECT count(*) AS decided_but_unfinished
   FROM "GameStats" s
   WHERE s."FinishedOnUtc" IS NULL
     AND (SELECT count(*) FROM "GameGuess" g WHERE g."GameSessionId" = s."Id") >= 6;
   ```

3. Deploy. `RepairMissingAnswers` fills unknown answers from the schedule, and `AddGameAnalytics` runs before it at startup. It adds two columns, three indexes, and the views, and takes brief locks
   only. The unique guess indexes cover new rows only, so old duplicates cannot fail it.
4. Compare `SELECT * FROM analytics.data_quality` with step 2.

Both are compatible with an old app instance that is still running, and so is `DropLegacySessionColumns`: it keeps
`DailyNumber` and `Mode` so the old version can still save guesses. After 2.0 is live, the next release drops them with
`ALTER TABLE "GameStats" DROP COLUMN IF EXISTS "DailyNumber", DROP COLUMN IF EXISTS "Mode";`.

### Adding unreleased characters

`AddCharacterAvailableFrom` hides ids from 60 on only if they exist when it runs, and 1.4.0 shows every character at
once. So insert the 2.0 characters **after** the deployment, with `available_from` in the same `INSERT`:

```sql
INSERT INTO "Characters" (..., available_from) VALUES (..., DATE '2026-12-01');

-- Must be 0
SELECT count(*) FROM "Characters" WHERE id >= 60 AND available_from IS NULL;
```

To undo, migrate back to `20261122174856_DropLegacySessionColumns`. This drops the views, rulesets, and guess times, and
keeps games and guesses.

### Optional read-only login

```sql
CREATE ROLE analytics_reader LOGIN PASSWORD '<password>';
GRANT USAGE ON SCHEMA analytics TO analytics_reader;
GRANT SELECT ON ALL TABLES IN SCHEMA analytics TO analytics_reader;
```

The views run with their owner's rights, so this login can't read the tables behind them. Query 7a also works with it.

## Known limitations

- Practice is not restored from the database. If recording its last guess fails, that game stays unfinished.
- A Practice game that crosses the premiere at midnight UTC switches clues mid-game, as the game rules do. Its ruleset is
  that of its first guess.
- Character data is not versioned, and a ruleset does not list the roster. Characters added after 2.0 stay `2.0`.
- Practice guesses recorded late have no time, and the start of a game whose first recording failed is the later one.
- Browsers are not people, and private windows inflate new browsers.
- A difficulty rank compares the players who chose to play that puzzle, so a small sample can still mislead. The sample
  size is always next to the rank.
