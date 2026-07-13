using Microsoft.EntityFrameworkCore;
using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Infrastructure.Persistence;

public sealed class DatabaseGameStatsStore(
    IDbContextFactory<ApplicationDbContext> DbContextFactory) : IGameStatsStore
{
    public async Task<IEnumerable<GameSession>> GetPlayerGames(Guid playerId)
    {
        await using var database = await DbContextFactory.CreateDbContextAsync();

        return await database.GameStats
            .AsNoTracking()
            .Where(game => game.PlayerId.Equals(playerId))
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

        return await database.Set<GlobalStatsView>()
            .AsNoTracking()
            .SingleAsync();
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

        var playerGameCounts = await database.GameStats
            .AsNoTracking()
            .Where(g => g.PlayerId != Guid.Empty)
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
                WHERE "PlayerId" != '00000000-0000-0000-0000-000000000000'
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
                )
            )
            SELECT 
                (SELECT COUNT(*) FROM EligibleForD1) as "EligibleD1",
                (SELECT COUNT(*) FROM ReturnedD1) as "ReturnedD1",
                (SELECT COUNT(*) FROM EligibleForD7) as "EligibleD7",
                (SELECT COUNT(*) FROM ReturnedD7) as "ReturnedD7"
            """;

        var result = await database.Database
            .SqlQueryRaw<RetentionStatsRaw>(sql)
            .SingleAsync();

        var d1Retention = result.EligibleD1 > 0 ? (result.ReturnedD1 * 100.0 / result.EligibleD1) : 0;
        var d7Retention = result.EligibleD7 > 0 ? (result.ReturnedD7 * 100.0 / result.EligibleD7) : 0;

        return new RetentionStats
        {
            D1Retention = d1Retention,
            D7Retention = d7Retention
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
    }
}
