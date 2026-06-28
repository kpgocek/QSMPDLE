using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QSMPDLE.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalStatsMaterializedView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE MATERIALIZED VIEW global_stats AS
                SELECT
                    1 AS id,
                    COUNT(*) AS total_games,
                    COUNT(DISTINCT "PlayerId") AS total_players,
                    COUNT(*) FILTER (WHERE "IsWon" = TRUE) AS total_wins
                FROM "GameStats";
            """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX ux_global_stats_id
                ON global_stats (id);
            """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS global_stats;");
        }
    }
}
