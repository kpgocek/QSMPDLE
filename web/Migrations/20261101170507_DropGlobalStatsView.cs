using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QSMPDLE.Web.Migrations;

/// <summary>
/// Nothing read global_stats; its five-minute non-concurrent refresh only cost a full scan and an
/// exclusive lock. Site-wide stats are now cached in memory by CachedGlobalStatsService.
/// </summary>
public partial class DropGlobalStatsView : Migration
{
    private const string EmptyPlayerId = "00000000-0000-0000-0000-000000000000";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS global_stats;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""
            CREATE MATERIALIZED VIEW global_stats AS
            SELECT
                1 AS id,
                COUNT(*) AS total_games,
                COUNT(DISTINCT "PlayerId") FILTER (WHERE "PlayerId" <> '{EmptyPlayerId}'::uuid) AS total_players,
                COUNT(*) FILTER (WHERE "IsWon" = TRUE) AS total_wins
            FROM "GameStats";
        """);

        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX ux_global_stats_id
            ON global_stats (id);
        """);
    }
}
