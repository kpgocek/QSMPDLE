using Microsoft.EntityFrameworkCore;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Infrastructure.Persistence;

public sealed class DatabaseGameStatsStore(
    IDbContextFactory<ApplicationDbContext> DbContextFactory) : IGameStatsStore
{
    public async Task<IEnumerable<GameSession>> GetPlayerGames(Guid playerId)
    {
        await using var database = await DbContextFactory.CreateDbContextAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return await database.GameStats
            .AsNoTracking()
            .Where(game => game.PlayerId.Equals(playerId))
            .Where(game => !(game.Mode == GameMode.Daily && DateOnly.FromDateTime(game.StartedOnUtc.UtcDateTime) == today))
            .ToListAsync();
    }

    public async Task<List<int>> GetPlayerCompletedDailyNumbersAsync(Guid playerId)
    {
        await using var database = await DbContextFactory.CreateDbContextAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var numbers = await database.GameStats
            .AsNoTracking()
            .Where(gs => gs.PlayerId == playerId
                         && gs.Mode == GameMode.Daily
                         && gs.FinishedOnUtc.HasValue
                         && !(gs.Mode == GameMode.Daily && DateOnly.FromDateTime(gs.StartedOnUtc.UtcDateTime) == today))
            .Select(gs => gs.DailyNumber)
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .Distinct()
            .ToListAsync();

        return numbers;
    }

    public async Task<List<GameSession>> GetPlayerDailyGamesByNumberRangeAsync(Guid playerId, int startNumber, int endNumber)
    {
        await using var database = await DbContextFactory.CreateDbContextAsync();

        // Single batched query for the DailyNumber range
        var sessions = await database.GameStats
            .AsNoTracking()
            .Where(gs => gs.PlayerId == playerId && gs.Mode == GameMode.Daily && gs.DailyNumber.HasValue && gs.DailyNumber >= startNumber && gs.DailyNumber <= endNumber)
            .ToListAsync();

        return sessions.Select(s => new GameSession
        {
            GameId = s.GameId,
            PlayerId = s.PlayerId,
            Mode = s.Mode,
            DailyNumber = s.DailyNumber,
            TargetCharacterId = s.TargetCharacterId,
            StartedOnUtc = s.StartedOnUtc,
            FinishedOnUtc = s.FinishedOnUtc,
            IsWon = s.IsWon,
            Guesses = s.Guesses.Select(g => new GameGuess
            {
                GameId = g.GameId,
                GuessOrder = g.GuessOrder,
                GuessedCharacterId = g.GuessedCharacterId
            }).ToList()
        }).ToList();
    }

    public async Task<GameSession?> GetPlayerCompletedDailyGameAsync(Guid playerId, int dailyNumber)
    {
        await using var database = await DbContextFactory.CreateDbContextAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return await database.GameStats
            .AsNoTracking()
            .Include(stats => stats.Guesses)
            .FirstOrDefaultAsync(game =>
                game.PlayerId == playerId &&
                game.Mode == GameMode.Daily &&
                game.DailyNumber == dailyNumber &&
                game.FinishedOnUtc.HasValue &&
                !(game.Mode == GameMode.Daily && DateOnly.FromDateTime(game.StartedOnUtc.UtcDateTime) == today));
    }

    public async Task<GameSession> LoadOrNewAsync(Guid gameId)
    {
        await using var database = await DbContextFactory.CreateDbContextAsync();

        var stats = await database.GameStats
            .AsNoTracking()
            .Include(stats => stats.Guesses)
            .FirstOrDefaultAsync(stats => stats.GameId == gameId);

        return stats ?? new GameSession { GameId = gameId };
    }

    public async Task SaveAsync(GameSession stats)
    {
        if (stats.PlayerId == Guid.Empty)
        {
            throw new InvalidOperationException("Cannot save game stats without a valid player id.");
        }

        await using var database = await DbContextFactory.CreateDbContextAsync();

        database.GameStats.Update(stats);

        await database.SaveChangesAsync();
    }

    public async Task<GlobalStatsView> GetGlobalStatsAsync()
    {
        await using var database = await DbContextFactory.CreateDbContextAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var query = database.GameStats
            .AsNoTracking()
            .Where(g => g.PlayerId != Guid.Empty)
            .Where(g => !(g.Mode == GameMode.Daily && DateOnly.FromDateTime(g.StartedOnUtc.UtcDateTime) == today));

        var totalGames = await query.CountAsync();
        var totalPlayers = await query.Select(g => g.PlayerId).Distinct().CountAsync();
        var totalWins = await query.CountAsync(g => g.IsWon);
        var totalCompletedGames = await query.CountAsync(g => g.FinishedOnUtc.HasValue);
        var avgGuessesToWin = await query
            .Where(g => g.IsWon)
            .Select(g => (double?)g.Guesses.Count)
            .AverageAsync() ?? 0;
        var avgGuessesPerCompletedGame = await query
            .Where(g => g.FinishedOnUtc.HasValue)
            .Select(g => (double?)g.Guesses.Count)
            .AverageAsync() ?? 0;

        var totalRecordedSessions = await query.CountAsync();
        var modePopularity = await query
            .GroupBy(g => g.Mode)
            .Select(group => new ModePopularityEntry
            {
                Mode = group.Key,
                SessionCount = group.LongCount(),
                Share = totalRecordedSessions == 0 ? 0 : group.LongCount() * 100.0 / totalRecordedSessions
            })
            .ToListAsync();

        // Sequential queries for guess distributions per mode (avoiding Task.WhenAll)
        var dailyDistribution = new long[6];
        var dailyDistributionData = await query
            .Where(g => g.Mode == GameMode.Daily && g.IsWon)
            .GroupBy(g => g.Guesses.Count)
            .Select(group => new { GuessCount = group.Key, Count = (long)group.Count() })
            .ToListAsync();

        foreach (var item in dailyDistributionData)
        {
            if (item.GuessCount >= 1 && item.GuessCount <= 6)
            {
                dailyDistribution[item.GuessCount - 1] = item.Count;
            }
        }

        var practiceDistribution = new long[6];
        var practiceDistributionData = await query
            .Where(g => g.Mode == GameMode.Practice && g.IsWon)
            .GroupBy(g => g.Guesses.Count)
            .Select(group => new { GuessCount = group.Key, Count = (long)group.Count() })
            .ToListAsync();

        foreach (var item in practiceDistributionData)
        {
            if (item.GuessCount >= 1 && item.GuessCount <= 6)
            {
                practiceDistribution[item.GuessCount - 1] = item.Count;
            }
        }

        return new GlobalStatsView
        {
            TotalGames = totalGames,
            TotalPlayers = totalPlayers,
            TotalWins = totalWins,
            TotalCompletedGames = totalCompletedGames,
            AverageGuessesToWin = avgGuessesToWin,
            AverageGuessesPerCompletedGame = avgGuessesPerCompletedGame,
            ModePopularity = modePopularity,
            DailyGuessDistribution = dailyDistribution,
            PracticeGuessDistribution = practiceDistribution
        };
    }

    public async Task<List<DailyActivePlayersData>> GetDailyActivePlayersAsync(DateOnly? from)
    {
        await using var database = await DbContextFactory.CreateDbContextAsync();

        var query = database.GameStats
            .AsNoTracking()
            .Where(g => g.PlayerId != Guid.Empty);

        if (from.HasValue)
        {
            var fromDateTime = from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(g => g.StartedOnUtc >= fromDateTime);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Exclude the current daily game (today's daily) from all stats
        query = query.Where(g => !(g.Mode == GameMode.Daily && DateOnly.FromDateTime(g.StartedOnUtc.UtcDateTime) == today));

        return await query
            .GroupBy(g => DateOnly.FromDateTime(g.StartedOnUtc.UtcDateTime))
            .Select(g => new DailyActivePlayersData
            {
                Date = g.Key,
                PlayerCount = g.Select(x => x.PlayerId).Distinct().Count()
            })
            .OrderBy(d => d.Date)
            .ToListAsync();
    }

    public async Task<List<NewVsReturningPlayersData>> GetNewVsReturningPlayersAsync(DateOnly? from)
    {
        await using var database = await DbContextFactory.CreateDbContextAsync();

        // Use separate queries for filtered vs all-time to avoid PostgreSQL parameter type issues
        if (from.HasValue)
        {
            var fromDateString = from.Value.ToString("yyyy-MM-dd");
            const string sql = """
                WITH PlayerFirstGame AS (
                    SELECT 
                        "PlayerId",
                        DATE(MIN("StartedOnUtc" AT TIME ZONE 'UTC')) as "FirstGameDate"
                    FROM "GameStats"
                    WHERE "PlayerId" != '00000000-0000-0000-0000-000000000000'
                      AND NOT ("Mode" = 0 AND DATE("StartedOnUtc" AT TIME ZONE 'UTC') = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date)
                    GROUP BY "PlayerId"
                ),
                DailyActivity AS (
                    SELECT DISTINCT
                        DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') as "ActivityDate",
                        gs."PlayerId",
                        pfg."FirstGameDate"
                    FROM "GameStats" gs
                    INNER JOIN PlayerFirstGame pfg ON gs."PlayerId" = pfg."PlayerId"
                    WHERE gs."PlayerId" != '00000000-0000-0000-0000-000000000000'
                      AND DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') >= {0}::date
                      AND NOT (gs."Mode" = 0 AND DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date)
                )
                SELECT 
                    "ActivityDate" as "Date",
                    COUNT(*) FILTER (WHERE "ActivityDate" = "FirstGameDate") as "NewPlayers",
                    COUNT(*) FILTER (WHERE "ActivityDate" > "FirstGameDate") as "ReturningPlayers"
                FROM DailyActivity
                GROUP BY "ActivityDate"
                ORDER BY "ActivityDate"
                """;

            var results = await database.Database
                .SqlQueryRaw<NewVsReturningPlayersDataRaw>(sql, fromDateString)
                .ToListAsync();

            return results.ConvertAll(r => new NewVsReturningPlayersData
            {
                Date = r.Date,
                NewPlayers = r.NewPlayers,
                ReturningPlayers = r.ReturningPlayers
            });
        }
        else
        {
            const string sql = """
                WITH PlayerFirstGame AS (
                    SELECT 
                        "PlayerId",
                        DATE(MIN("StartedOnUtc" AT TIME ZONE 'UTC')) as "FirstGameDate"
                    FROM "GameStats"
                    WHERE "PlayerId" != '00000000-0000-0000-0000-000000000000'
                      AND NOT ("Mode" = 0 AND DATE("StartedOnUtc" AT TIME ZONE 'UTC') = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date)
                    GROUP BY "PlayerId"
                ),
                DailyActivity AS (
                    SELECT DISTINCT
                        DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') as "ActivityDate",
                        gs."PlayerId",
                        pfg."FirstGameDate"
                    FROM "GameStats" gs
                    INNER JOIN PlayerFirstGame pfg ON gs."PlayerId" = pfg."PlayerId"
                    WHERE gs."PlayerId" != '00000000-0000-0000-0000-000000000000'
                      AND NOT (gs."Mode" = 0 AND DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date)
                )
                SELECT 
                    "ActivityDate" as "Date",
                    COUNT(*) FILTER (WHERE "ActivityDate" = "FirstGameDate") as "NewPlayers",
                    COUNT(*) FILTER (WHERE "ActivityDate" > "FirstGameDate") as "ReturningPlayers"
                FROM DailyActivity
                GROUP BY "ActivityDate"
                ORDER BY "ActivityDate"
                """;

            var results = await database.Database
                .SqlQueryRaw<NewVsReturningPlayersDataRaw>(sql)
                .ToListAsync();

            return results.ConvertAll(r => new NewVsReturningPlayersData
            {
                Date = r.Date,
                NewPlayers = r.NewPlayers,
                ReturningPlayers = r.ReturningPlayers
            });
        }
    }

    public async Task<PlayerActivityDistributionData[]> GetPlayerActivityDistributionAsync()
    {
        await using var database = await DbContextFactory.CreateDbContextAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var playerGameCounts = await database.GameStats
            .AsNoTracking()
            .Where(g => g.PlayerId != Guid.Empty)
            .Where(g => !(g.Mode == GameMode.Daily && DateOnly.FromDateTime(g.StartedOnUtc.UtcDateTime) == today))
            .GroupBy(g => g.PlayerId)
            .Select(g => new { PlayerId = g.Key, GameCount = g.Count() })
            .ToListAsync();

        var buckets = new[]
        {
            ("1", playerGameCounts.Count(p => p.GameCount == 1)),
            ("2-5", playerGameCounts.Count(p => p.GameCount >= 2 && p.GameCount <= 5)),
            ("6-10", playerGameCounts.Count(p => p.GameCount >= 6 && p.GameCount <= 10)),
            ("11-25", playerGameCounts.Count(p => p.GameCount >= 11 && p.GameCount <= 25)),
            ("26-50", playerGameCounts.Count(p => p.GameCount >= 26 && p.GameCount <= 50)),
            ("51+", playerGameCounts.Count(p => p.GameCount >= 51))
        };

        return buckets.Select(b => new PlayerActivityDistributionData
        {
            Bucket = b.Item1,
            PlayerCount = b.Item2
        }).ToArray();
    }

    public async Task<GamesPerPlayerStats> GetGamesPerPlayerStatsAsync()
    {
        await using var database = await DbContextFactory.CreateDbContextAsync();

        const string sql = """
            WITH PlayerGameCounts AS (
                SELECT 
                    "PlayerId",
                    COUNT(*) as "GameCount"
                FROM "GameStats"
                WHERE "PlayerId" != '00000000-0000-0000-0000-000000000000'
                  AND NOT ("Mode" = 0 AND DATE("StartedOnUtc" AT TIME ZONE 'UTC') = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date)
                GROUP BY "PlayerId"
            )
            SELECT 
                AVG("GameCount")::float as "Average",
                PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY "GameCount")::float as "Median",
                MAX("GameCount") as "Maximum"
            FROM PlayerGameCounts
            """;

        var result = await database.Database
            .SqlQueryRaw<GamesPerPlayerStatsRaw>(sql)
            .SingleAsync();

        return new GamesPerPlayerStats
        {
            Average = result.Average,
            Median = result.Median,
            Maximum = result.Maximum
        };
    }

    public async Task<RetentionStats> GetRetentionStatsAsync()
    {
        await using var database = await DbContextFactory.CreateDbContextAsync();

        const string sql = """
            WITH PlayerCohorts AS (
                SELECT 
                    "PlayerId",
                    DATE(MIN("StartedOnUtc" AT TIME ZONE 'UTC')) as "CohortDate"
                FROM "GameStats"
                WHERE "PlayerId" != '00000000-0000-0000-0000-000000000000'
                  AND NOT ("Mode" = 0 AND DATE("StartedOnUtc" AT TIME ZONE 'UTC') = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date)
                GROUP BY "PlayerId"
            ),
            EligibleForD1 AS (
                SELECT "PlayerId", "CohortDate"
                FROM PlayerCohorts
                WHERE "CohortDate" <= (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date - INTERVAL '1 day'
            ),
            ReturnedD1 AS (
                SELECT DISTINCT e."PlayerId"
                FROM EligibleForD1 e
                WHERE EXISTS (
                    SELECT 1 FROM "GameStats" gs
                    WHERE gs."PlayerId" = e."PlayerId"
                      AND DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') = e."CohortDate" + INTERVAL '1 day'
                      AND NOT (gs."Mode" = 0 AND DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date)
                )
            ),
            EligibleForD7 AS (
                SELECT "PlayerId", "CohortDate"
                FROM PlayerCohorts
                WHERE "CohortDate" <= (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date - INTERVAL '7 days'
            ),
            ReturnedD7 AS (
                SELECT DISTINCT e."PlayerId"
                FROM EligibleForD7 e
                WHERE EXISTS (
                    SELECT 1 FROM "GameStats" gs
                    WHERE gs."PlayerId" = e."PlayerId"
                      AND DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') = e."CohortDate" + INTERVAL '7 days'
                      AND NOT (gs."Mode" = 0 AND DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date)
                )
            ),
            EligibleForD30 AS (
                SELECT "PlayerId", "CohortDate"
                FROM PlayerCohorts
                WHERE "CohortDate" <= (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date - INTERVAL '30 days'
            ),
            ReturnedD30 AS (
                SELECT DISTINCT e."PlayerId"
                FROM EligibleForD30 e
                WHERE EXISTS (
                    SELECT 1 FROM "GameStats" gs
                    WHERE gs."PlayerId" = e."PlayerId"
                      AND DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') = e."CohortDate" + INTERVAL '30 days'
                      AND NOT (gs."Mode" = 0 AND DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date)
                )
            ),
            ReturnedD1_Plus AS (
                SELECT DISTINCT e."PlayerId"
                FROM EligibleForD1 e
                WHERE EXISTS (
                    SELECT 1 FROM "GameStats" gs
                    WHERE gs."PlayerId" = e."PlayerId"
                      AND DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') >= e."CohortDate" + INTERVAL '1 day'
                      AND NOT (gs."Mode" = 0 AND DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date)
                )
            ),
            ReturnedD7_Plus AS (
                SELECT DISTINCT e."PlayerId"
                FROM EligibleForD7 e
                WHERE EXISTS (
                    SELECT 1 FROM "GameStats" gs
                    WHERE gs."PlayerId" = e."PlayerId"
                      AND DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') >= e."CohortDate" + INTERVAL '7 days'
                      AND NOT (gs."Mode" = 0 AND DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date)
                )
            ),
            ReturnedD30_Plus AS (
                SELECT DISTINCT e."PlayerId"
                FROM EligibleForD30 e
                WHERE EXISTS (
                    SELECT 1 FROM "GameStats" gs
                    WHERE gs."PlayerId" = e."PlayerId"
                      AND DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') >= e."CohortDate" + INTERVAL '30 days'
                      AND NOT (gs."Mode" = 0 AND DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date)
                )
            )
            SELECT 
                (SELECT COUNT(*) FROM EligibleForD1) as "EligibleD1",
                (SELECT COUNT(*) FROM ReturnedD1) as "ReturnedD1",
                (SELECT COUNT(*) FROM EligibleForD7) as "EligibleD7",
                (SELECT COUNT(*) FROM ReturnedD7) as "ReturnedD7",
                (SELECT COUNT(*) FROM EligibleForD30) as "EligibleD30",
                (SELECT COUNT(*) FROM ReturnedD30) as "ReturnedD30",
                (SELECT COUNT(*) FROM EligibleForD1) as "EligibleD1Plus",
                (SELECT COUNT(*) FROM ReturnedD1_Plus) as "ReturnedD1Plus",
                (SELECT COUNT(*) FROM EligibleForD7) as "EligibleD7Plus",
                (SELECT COUNT(*) FROM ReturnedD7_Plus) as "ReturnedD7Plus",
                (SELECT COUNT(*) FROM EligibleForD30) as "EligibleD30Plus",
                (SELECT COUNT(*) FROM ReturnedD30_Plus) as "ReturnedD30Plus"
            """;

        var result = await database.Database
            .SqlQueryRaw<RetentionStatsRaw>(sql)
            .SingleAsync();

        var d1Retention = result.EligibleD1 > 0 ? (result.ReturnedD1 * 100.0 / result.EligibleD1) : 0;
        var d7Retention = result.EligibleD7 > 0 ? (result.ReturnedD7 * 100.0 / result.EligibleD7) : 0;
        var d30Retention = result.EligibleD30 > 0 ? (result.ReturnedD30 * 100.0 / result.EligibleD30) : 0;

        var d1PlusRetention = result.EligibleD1Plus > 0 ? (result.ReturnedD1Plus * 100.0 / result.EligibleD1Plus) : 0;
        var d7PlusRetention = result.EligibleD7Plus > 0 ? (result.ReturnedD7Plus * 100.0 / result.EligibleD7Plus) : 0;
        var d30PlusRetention = result.EligibleD30Plus > 0 ? (result.ReturnedD30Plus * 100.0 / result.EligibleD30Plus) : 0;

        return new RetentionStats
        {
            D1Retention = d1Retention,
            D7Retention = d7Retention,
            D30Retention = d30Retention,
            D1PlusRetention = d1PlusRetention,
            D7PlusRetention = d7PlusRetention,
            D30PlusRetention = d30PlusRetention
        };
    }

    public async Task<GlobalCharacterStats> GetGlobalCharacterStatsAsync()
    {
        await using var database = await DbContextFactory.CreateDbContextAsync();

        // Most confusing: character that appeared as a WRONG guess most often (guessed but not the target)
        const string mostConfusingSql = """
            WITH base AS (
                SELECT gg."GuessedCharacterId" AS "CharacterId",
                       gs."StartedOnUtc"
                FROM "GameGuess" gg
                INNER JOIN "GameStats" gs ON gg."GameId" = gs."GameId"
                WHERE gs."PlayerId" != '00000000-0000-0000-0000-000000000000'
                  AND gg."GuessedCharacterId" != gs."TargetCharacterId"
                  AND NOT (gs."Mode" = 0 AND DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date)
            ),
            counts AS (
                SELECT "CharacterId",
                    COUNT(*) AS "CountAll",
                    COUNT(*) FILTER (WHERE DATE("StartedOnUtc" AT TIME ZONE 'UTC') >= (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date - INTERVAL '30 days') AS "Count30",
                    COUNT(*) FILTER (WHERE DATE("StartedOnUtc" AT TIME ZONE 'UTC') >= (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date - INTERVAL '14 days') AS "Count14"
                FROM base
                GROUP BY "CharacterId"
            ),
            ranked AS (
                SELECT *,
                    ROW_NUMBER() OVER (ORDER BY "CountAll" DESC) AS "RnAll",
                    ROW_NUMBER() OVER (ORDER BY "Count30"  DESC) AS "Rn30",
                    ROW_NUMBER() OVER (ORDER BY "Count14"  DESC) AS "Rn14"
                FROM counts
            )
            SELECT "CharacterId", "CountAll", "Count30", "Count14", "RnAll", "Rn30", "Rn14"
            FROM ranked
            WHERE "RnAll" <= 3 OR "Rn30" <= 3 OR "Rn14" <= 3
            """;

        // Easiest: target character most frequently guessed correctly when won
        const string easiestSql = """
            WITH base AS (
                SELECT gs."TargetCharacterId" AS "CharacterId",
                       gs."StartedOnUtc"
                FROM "GameStats" gs
                WHERE gs."PlayerId" != '00000000-0000-0000-0000-000000000000'
                  AND gs."IsWon" = TRUE
                  AND NOT (gs."Mode" = 0 AND DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date)
            ),
            counts AS (
                SELECT "CharacterId",
                    COUNT(*) AS "CountAll",
                    COUNT(*) FILTER (WHERE DATE("StartedOnUtc" AT TIME ZONE 'UTC') >= (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date - INTERVAL '30 days') AS "Count30",
                    COUNT(*) FILTER (WHERE DATE("StartedOnUtc" AT TIME ZONE 'UTC') >= (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date - INTERVAL '14 days') AS "Count14"
                FROM base
                GROUP BY "CharacterId"
            ),
            ranked AS (
                SELECT *,
                    ROW_NUMBER() OVER (ORDER BY "CountAll" DESC) AS "RnAll",
                    ROW_NUMBER() OVER (ORDER BY "Count30"  DESC) AS "Rn30",
                    ROW_NUMBER() OVER (ORDER BY "Count14"  DESC) AS "Rn14"
                FROM counts
            )
            SELECT "CharacterId", "CountAll", "Count30", "Count14", "RnAll", "Rn30", "Rn14"
            FROM ranked
            WHERE "RnAll" <= 3 OR "Rn30" <= 3 OR "Rn14" <= 3
            """;

        // The indicator: character guess that was immediately followed by a win on the next guess
        const string indicatorSql = """
            WITH base AS (
                SELECT gg."GuessedCharacterId" AS "CharacterId",
                       gs."StartedOnUtc"
                FROM "GameGuess" gg
                INNER JOIN "GameStats" gs ON gg."GameId" = gs."GameId"
                WHERE gs."PlayerId" != '00000000-0000-0000-0000-000000000000'
                  AND gs."IsWon" = TRUE
                  AND gg."GuessedCharacterId" != gs."TargetCharacterId"
                  AND NOT EXISTS (
                      SELECT 1 FROM "GameGuess" gg2
                      WHERE gg2."GameId" = gg."GameId"
                        AND gg2."GuessOrder" < gg."GuessOrder"
                        AND gg2."GuessedCharacterId" = gs."TargetCharacterId"
                  )
                  AND EXISTS (
                      SELECT 1 FROM "GameGuess" gg3
                      WHERE gg3."GameId" = gg."GameId"
                        AND gg3."GuessOrder" = gg."GuessOrder" + 1
                        AND gg3."GuessedCharacterId" = gs."TargetCharacterId"
                  )
                  AND NOT (gs."Mode" = 0 AND DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date)
            ),
            counts AS (
                SELECT "CharacterId",
                    COUNT(*) AS "CountAll",
                    COUNT(*) FILTER (WHERE DATE("StartedOnUtc" AT TIME ZONE 'UTC') >= (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date - INTERVAL '30 days') AS "Count30",
                    COUNT(*) FILTER (WHERE DATE("StartedOnUtc" AT TIME ZONE 'UTC') >= (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date - INTERVAL '14 days') AS "Count14"
                FROM base
                GROUP BY "CharacterId"
            ),
            ranked AS (
                SELECT *,
                    ROW_NUMBER() OVER (ORDER BY "CountAll" DESC) AS "RnAll",
                    ROW_NUMBER() OVER (ORDER BY "Count30"  DESC) AS "Rn30",
                    ROW_NUMBER() OVER (ORDER BY "Count14"  DESC) AS "Rn14"
                FROM counts
            )
            SELECT "CharacterId", "CountAll", "Count30", "Count14", "RnAll", "Rn30", "Rn14"
            FROM ranked
            WHERE "RnAll" <= 3 OR "Rn30" <= 3 OR "Rn14" <= 3
            """;

        // The opener: most popular first guess (GuessOrder=0) in winning sessions
        const string openerSql = """
            WITH base AS (
                SELECT gg."GuessedCharacterId" AS "CharacterId",
                       gs."StartedOnUtc"
                FROM "GameGuess" gg
                INNER JOIN "GameStats" gs ON gg."GameId" = gs."GameId"
                WHERE gs."PlayerId" != '00000000-0000-0000-0000-000000000000'
                  AND gs."IsWon" = TRUE
                  AND gg."GuessOrder" = 0
                  AND gg."GuessedCharacterId" != gs."TargetCharacterId"
                  AND NOT (gs."Mode" = 0 AND DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date)
            ),
            counts AS (
                SELECT "CharacterId",
                    COUNT(*) AS "CountAll",
                    COUNT(*) FILTER (WHERE DATE("StartedOnUtc" AT TIME ZONE 'UTC') >= (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date - INTERVAL '30 days') AS "Count30",
                    COUNT(*) FILTER (WHERE DATE("StartedOnUtc" AT TIME ZONE 'UTC') >= (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date - INTERVAL '14 days') AS "Count14"
                FROM base
                GROUP BY "CharacterId"
            ),
            ranked AS (
                SELECT *,
                    ROW_NUMBER() OVER (ORDER BY "CountAll" DESC) AS "RnAll",
                    ROW_NUMBER() OVER (ORDER BY "Count30"  DESC) AS "Rn30",
                    ROW_NUMBER() OVER (ORDER BY "Count14"  DESC) AS "Rn14"
                FROM counts
            )
            SELECT "CharacterId", "CountAll", "Count30", "Count14", "RnAll", "Rn30", "Rn14"
            FROM ranked
            WHERE "RnAll" <= 3 OR "Rn30" <= 3 OR "Rn14" <= 3
            """;

        // Hardest: target characters that required the most guesses on average in won games
        const string hardestSql = """
            WITH base AS (
                SELECT gs."TargetCharacterId" AS "CharacterId",
                       gs."StartedOnUtc",
                       COUNT(gg."Id") AS "GuessCount"
                FROM "GameStats" gs
                INNER JOIN "GameGuess" gg ON gg."GameId" = gs."GameId"
                WHERE gs."PlayerId" != '00000000-0000-0000-0000-000000000000'
                  AND gs."IsWon" = TRUE
                  AND NOT (gs."Mode" = 0 AND DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date)
                GROUP BY gs."GameId", gs."TargetCharacterId", gs."StartedOnUtc"
            ),
            counts AS (
                SELECT "CharacterId",
                    AVG("GuessCount") AS "CountAll",
                    AVG("GuessCount") FILTER (WHERE DATE("StartedOnUtc" AT TIME ZONE 'UTC') >= (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date - INTERVAL '30 days') AS "Count30",
                    AVG("GuessCount") FILTER (WHERE DATE("StartedOnUtc" AT TIME ZONE 'UTC') >= (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date - INTERVAL '14 days') AS "Count14"
                FROM base
                GROUP BY "CharacterId"
            ),
            ranked AS (
                SELECT *,
                    ROW_NUMBER() OVER (ORDER BY "CountAll" DESC NULLS LAST) AS "RnAll",
                    ROW_NUMBER() OVER (ORDER BY "Count30"  DESC NULLS LAST) AS "Rn30",
                    ROW_NUMBER() OVER (ORDER BY "Count14"  DESC NULLS LAST) AS "Rn14"
                FROM counts
            )
            SELECT "CharacterId",
                ROUND("CountAll"::numeric, 1) AS "CountAll",
                ROUND(COALESCE("Count30", 0)::numeric, 1) AS "Count30",
                ROUND(COALESCE("Count14", 0)::numeric, 1) AS "Count14",
                "RnAll", "Rn30", "Rn14"
            FROM ranked
            WHERE "RnAll" <= 3 OR "Rn30" <= 3 OR "Rn14" <= 3
            """;

        static CharacterWindowStats BuildWindowStats(List<CharacterStatEntryMultiWindowRaw> rows)
        {
            static CharacterStatEntry Map(CharacterStatEntryMultiWindowRaw r, long count) =>
                new() { CharacterId = r.CharacterId, CharacterName = r.CharacterId.ToString(), Count = count };

            return new CharacterWindowStats
            {
                PastTwoWeeks = rows.Where(r => r.Rn14 <= 3).OrderBy(r => r.Rn14).Select(r => Map(r, r.Count14)).ToList(),
                PastMonth = rows.Where(r => r.Rn30 <= 3).OrderBy(r => r.Rn30).Select(r => Map(r, r.Count30)).ToList(),
                AllTime = rows.Where(r => r.RnAll <= 3).OrderBy(r => r.RnAll).Select(r => Map(r, r.CountAll)).ToList(),
            };
        }

        var confusingRows = await database.Database.SqlQueryRaw<CharacterStatEntryMultiWindowRaw>(mostConfusingSql).ToListAsync();
        var easiestRows = await database.Database.SqlQueryRaw<CharacterStatEntryMultiWindowRaw>(easiestSql).ToListAsync();
        var hardestRows = await database.Database.SqlQueryRaw<CharacterStatEntryMultiWindowRaw>(hardestSql).ToListAsync();
        var indicatorRows = await database.Database.SqlQueryRaw<CharacterStatEntryMultiWindowRaw>(indicatorSql).ToListAsync();
        var openerRows = await database.Database.SqlQueryRaw<CharacterStatEntryMultiWindowRaw>(openerSql).ToListAsync();

        return new GlobalCharacterStats
        {
            MostConfusing = BuildWindowStats(confusingRows),
            MostWonTargets = BuildWindowStats(easiestRows),
            Hardest = BuildWindowStats(hardestRows),
            TheIndicator = BuildWindowStats(indicatorRows),
            TheOpener = BuildWindowStats(openerRows),
        };
    }

    public async Task<PlayerCharacterStats> GetPlayerCharacterStatsAsync(Guid playerId)
    {
        await using var database = await DbContextFactory.CreateDbContextAsync();

        // Most guessed (as wrong guesses)
        const string mostGuessedSql = """
            SELECT 
                gg."GuessedCharacterId" as "CharacterId",
                COUNT(*) as "Count"
            FROM "GameGuess" gg
            INNER JOIN "GameStats" gs ON gg."GameId" = gs."GameId"
            WHERE gs."PlayerId" = {0}
              AND gg."GuessedCharacterId" != gs."TargetCharacterId"
              AND NOT (gs."Mode" = 0 AND DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date)
            GROUP BY gg."GuessedCharacterId"
            ORDER BY "Count" DESC
            LIMIT 1
            """;

        // Most correctly guessed = character was the target and the game was won
        const string mostCorrectSql = """
            SELECT 
                gs."TargetCharacterId" as "CharacterId",
                COUNT(*) as "Count"
            FROM "GameStats" gs
            WHERE gs."PlayerId" = {0}
              AND gs."IsWon" = TRUE
              AND NOT (gs."Mode" = 0 AND DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date)
            GROUP BY gs."TargetCharacterId"
            ORDER BY "Count" DESC
            LIMIT 1
            """;

        var mostGuessedResults = await database.Database
            .SqlQueryRaw<CharacterStatEntryRaw>(mostGuessedSql, playerId)
            .ToListAsync();

        var mostCorrectResults = await database.Database
            .SqlQueryRaw<CharacterStatEntryRaw>(mostCorrectSql, playerId)
            .ToListAsync();

        var mostGuessed = mostGuessedResults.FirstOrDefault();
        var mostCorrect = mostCorrectResults.FirstOrDefault();

        return new PlayerCharacterStats
        {
            MostGuessedCharacterId = mostGuessed?.CharacterId,
            MostGuessedCharacterName = null,
            MostGuessedCount = mostGuessed?.Count ?? 0,
            MostCorrectlyGuessedCharacterId = mostCorrect?.CharacterId,
            MostCorrectlyGuessedCharacterName = null,
            MostCorrectlyGuessedCount = mostCorrect?.Count ?? 0,
        };
    }

    public async Task<DateOnly?> GetPlayerFirstGameDateAsync(Guid playerId)
    {
        await using var database = await DbContextFactory.CreateDbContextAsync();

        var earliest = await database.GameStats
            .AsNoTracking()
            .Where(g => g.PlayerId == playerId)
            .Select(g => (DateTimeOffset?)g.StartedOnUtc)
            .MinAsync();

        return earliest.HasValue
            ? DateOnly.FromDateTime(earliest.Value.UtcDateTime)
            : null;
    }

    private sealed class NewVsReturningPlayersDataRaw
    {
        public DateOnly Date { get; set; }
        public long NewPlayers { get; set; }
        public long ReturningPlayers { get; set; }
    }

    private sealed class GamesPerPlayerStatsRaw
    {
        public double Average { get; set; }
        public double Median { get; set; }
        public long Maximum { get; set; }
    }

    private sealed class RetentionStatsRaw
    {
        public long EligibleD1 { get; set; }
        public long ReturnedD1 { get; set; }
        public long EligibleD7 { get; set; }
        public long ReturnedD7 { get; set; }
        public long EligibleD30 { get; set; }
        public long ReturnedD30 { get; set; }

        public long EligibleD1Plus { get; set; }
        public long ReturnedD1Plus { get; set; }
        public long EligibleD7Plus { get; set; }
        public long ReturnedD7Plus { get; set; }
        public long EligibleD30Plus { get; set; }
        public long ReturnedD30Plus { get; set; }
    }

    private sealed class CharacterStatEntryRaw
    {
        public int CharacterId { get; set; }
        public long Count { get; set; }
    }

    private sealed class CharacterStatEntryMultiWindowRaw
    {
        public int CharacterId { get; set; }
        public long CountAll { get; set; }
        public long Count30 { get; set; }
        public long Count14 { get; set; }
        public long RnAll { get; set; }
        public long Rn30 { get; set; }
        public long Rn14 { get; set; }
    }
}
