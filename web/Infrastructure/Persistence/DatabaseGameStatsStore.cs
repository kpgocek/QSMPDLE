using Microsoft.EntityFrameworkCore;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Features.Gameplay.Models;

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

        return new GlobalStatsView
        {
            TotalGames = totalGames,
            TotalPlayers = totalPlayers,
            TotalWins = totalWins
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
            var sql = """
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

            return results.Select(r => new NewVsReturningPlayersData
            {
                Date = r.Date,
                NewPlayers = r.NewPlayers,
                ReturningPlayers = r.ReturningPlayers
            }).ToList();
        }
        else
        {
            var sql = """
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

            return results.Select(r => new NewVsReturningPlayersData
            {
                Date = r.Date,
                NewPlayers = r.NewPlayers,
                ReturningPlayers = r.ReturningPlayers
            }).ToList();
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

        var sql = """
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

        var sql = """
            WITH PlayerCohorts AS (
                SELECT 
                    "PlayerId",
                    DATE(MIN("StartedOnUtc" AT TIME ZONE 'UTC')) as "CohortDate"
                FROM "GameStats"
                WHERE "PlayerId" != '00000000-0000-0000-000000000000'
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
            EligibleForD14 AS (
                SELECT "PlayerId", "CohortDate"
                FROM PlayerCohorts
                WHERE "CohortDate" <= (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date - INTERVAL '14 days'
            ),
            ReturnedD14 AS (
                SELECT DISTINCT e."PlayerId"
                FROM EligibleForD14 e
                WHERE EXISTS (
                    SELECT 1 FROM "GameStats" gs
                    WHERE gs."PlayerId" = e."PlayerId"
                      AND DATE(gs."StartedOnUtc" AT TIME ZONE 'UTC') = e."CohortDate" + INTERVAL '14 days'
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
            )
            SELECT 
                (SELECT COUNT(*) FROM EligibleForD1) as "EligibleD1",
                (SELECT COUNT(*) FROM ReturnedD1) as "ReturnedD1",
                (SELECT COUNT(*) FROM EligibleForD7) as "EligibleD7",
                (SELECT COUNT(*) FROM ReturnedD7) as "ReturnedD7",
                (SELECT COUNT(*) FROM EligibleForD14) as "EligibleD14",
                (SELECT COUNT(*) FROM ReturnedD14) as "ReturnedD14",
                (SELECT COUNT(*) FROM EligibleForD30) as "EligibleD30",
                (SELECT COUNT(*) FROM ReturnedD30) as "ReturnedD30"
            """;

        var result = await database.Database
            .SqlQueryRaw<RetentionStatsRaw>(sql)
            .SingleAsync();

        var d1Retention = result.EligibleD1 > 0 ? (result.ReturnedD1 * 100.0 / result.EligibleD1) : 0;
        var d7Retention = result.EligibleD7 > 0 ? (result.ReturnedD7 * 100.0 / result.EligibleD7) : 0;
        var d14Retention = result.EligibleD14 > 0 ? (result.ReturnedD14 * 100.0 / result.EligibleD14) : 0;
        var d30Retention = result.EligibleD30 > 0 ? (result.ReturnedD30 * 100.0 / result.EligibleD30) : 0;

        return new RetentionStats
        {
            D1Retention = d1Retention,
            D7Retention = d7Retention,
            D14Retention = d14Retention,
            D30Retention = d30Retention
        };
    }

    public async Task<GlobalCharacterStats> GetGlobalCharacterStatsAsync()
    {
        await using var database = await DbContextFactory.CreateDbContextAsync();

        // Most confusing: character that appeared as a WRONG guess most often (guessed but not the target)
        var mostConfusingSql = """
            SELECT 
                gg."GuessedCharacterId" as "CharacterId",
                COUNT(*) as "Count"
            FROM "GameGuess" gg
            INNER JOIN "GameStats" gs ON gg."GameId" = gs."GameId"
            WHERE gs."PlayerId" != '00000000-0000-0000-0000-000000000000'
              AND gg."GuessedCharacterId" != gs."TargetCharacterId"
              AND {0}
            GROUP BY gg."GuessedCharacterId"
            ORDER BY "Count" DESC
            LIMIT 3
            """;

        // Easiest: target character guessed correctly on fewest guesses (min avg guess count when won)
        var easiestSql = """
            SELECT 
                gs."TargetCharacterId" as "CharacterId",
                COUNT(*) as "Count"
            FROM "GameStats" gs
            WHERE gs."PlayerId" != '00000000-0000-0000-0000-000000000000'
              AND gs."IsWon" = TRUE
              AND {0}
            GROUP BY gs."TargetCharacterId"
            ORDER BY "Count" DESC
            LIMIT 3
            """;

        // The indicator: character guess that was immediately followed by a win on next guess
        var indicatorSql = """
            SELECT 
                gg."GuessedCharacterId" as "CharacterId",
                COUNT(*) as "Count"
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
              AND {0}
            GROUP BY gg."GuessedCharacterId"
            ORDER BY "Count" DESC
            LIMIT 3
            """;

        // The opener: most popular first guess (GuessOrder=0) in winning sessions
        var openerSql = """
            SELECT 
                gg."GuessedCharacterId" as "CharacterId",
                COUNT(*) as "Count"
            FROM "GameGuess" gg
            INNER JOIN "GameStats" gs ON gg."GameId" = gs."GameId"
            WHERE gs."PlayerId" != '00000000-0000-0000-0000-000000000000'
              AND gs."IsWon" = TRUE
              AND gg."GuessOrder" = 0
              AND gg."GuessedCharacterId" != gs."TargetCharacterId"
              AND {0}
            GROUP BY gg."GuessedCharacterId"
            ORDER BY "Count" DESC
            LIMIT 3
            """;

        var exclusion = "AND NOT (gs.\"Mode\" = 0 AND DATE(gs.\"StartedOnUtc\" AT TIME ZONE 'UTC') = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date)";

        var yesterdayFilter = $"DATE(gs.\"StartedOnUtc\" AT TIME ZONE 'UTC') = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date - INTERVAL '1 day' {exclusion}";
        var weekFilter = $"DATE(gs.\"StartedOnUtc\" AT TIME ZONE 'UTC') >= (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date - INTERVAL '7 days' {exclusion}";
        var monthFilter = $"DATE(gs.\"StartedOnUtc\" AT TIME ZONE 'UTC') >= (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date - INTERVAL '30 days' {exclusion}";

        async Task<List<CharacterStatEntryRaw>> RunQuery(string sqlTemplate, string dateFilter)
        {
            var sql = string.Format(sqlTemplate, dateFilter);
            return await database.Database.SqlQueryRaw<CharacterStatEntryRaw>(sql).ToListAsync();
        }

        CharacterWindowStats BuildWindowStats(
            List<CharacterStatEntryRaw> yesterday,
            List<CharacterStatEntryRaw> week,
            List<CharacterStatEntryRaw> month)
        {
            return new CharacterWindowStats
            {
                Yesterday = yesterday.Select(r => new CharacterStatEntry { CharacterId = r.CharacterId, CharacterName = r.CharacterId.ToString(), Count = r.Count }).ToList(),
                PastWeek = week.Select(r => new CharacterStatEntry { CharacterId = r.CharacterId, CharacterName = r.CharacterId.ToString(), Count = r.Count }).ToList(),
                PastMonth = month.Select(r => new CharacterStatEntry { CharacterId = r.CharacterId, CharacterName = r.CharacterId.ToString(), Count = r.Count }).ToList(),
            };
        }

        var confusingYesterday = await RunQuery(mostConfusingSql, yesterdayFilter);
        var confusingWeek = await RunQuery(mostConfusingSql, weekFilter);
        var confusingMonth = await RunQuery(mostConfusingSql, monthFilter);

        var easiestYesterday = await RunQuery(easiestSql, yesterdayFilter);
        var easiestWeek = await RunQuery(easiestSql, weekFilter);
        var easiestMonth = await RunQuery(easiestSql, monthFilter);

        var indicatorYesterday = await RunQuery(indicatorSql, yesterdayFilter);
        var indicatorWeek = await RunQuery(indicatorSql, weekFilter);
        var indicatorMonth = await RunQuery(indicatorSql, monthFilter);

        var openerYesterday = await RunQuery(openerSql, yesterdayFilter);
        var openerWeek = await RunQuery(openerSql, weekFilter);
        var openerMonth = await RunQuery(openerSql, monthFilter);

        return new GlobalCharacterStats
        {
            MostConfusing = BuildWindowStats(confusingYesterday, confusingWeek, confusingMonth),
            Easiest = BuildWindowStats(easiestYesterday, easiestWeek, easiestMonth),
            TheIndicator = BuildWindowStats(indicatorYesterday, indicatorWeek, indicatorMonth),
            TheOpener = BuildWindowStats(openerYesterday, openerWeek, openerMonth),
        };
    }

    public async Task<PlayerCharacterStats> GetPlayerCharacterStatsAsync(Guid playerId)
    {
        await using var database = await DbContextFactory.CreateDbContextAsync();

        // Most guessed (as wrong guesses)
        var mostGuessedSql = """
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
        var mostCorrectSql = """
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
            MostGuessedCharacterName = mostGuessed?.CharacterId.ToString(),
            MostGuessedCount = mostGuessed?.Count ?? 0,
            MostCorrectlyGuessedCharacterId = mostCorrect?.CharacterId,
            MostCorrectlyGuessedCharacterName = mostCorrect?.CharacterId.ToString(),
            MostCorrectlyGuessedCount = mostCorrect?.Count ?? 0,
        };
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
        public long EligibleD14 { get; set; }
        public long ReturnedD14 { get; set; }
        public long EligibleD30 { get; set; }
        public long ReturnedD30 { get; set; }
    }

    private sealed class CharacterStatEntryRaw
    {
        public int CharacterId { get; set; }
        public long Count { get; set; }
    }
}
