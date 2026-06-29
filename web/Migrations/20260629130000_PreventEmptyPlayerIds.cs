using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QSMPDLE.Web.Infrastructure.Persistence;

#nullable disable

namespace QSMPDLE.Web.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260629130000_PreventEmptyPlayerIds")]
    public partial class PreventEmptyPlayerIds : Migration
    {
        private const string EmptyPlayerId = "00000000-0000-0000-0000-000000000000";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                ALTER TABLE "GameStats"
                ADD CONSTRAINT "CK_GameStats_PlayerId_NotEmpty"
                CHECK ("PlayerId" <> '{EmptyPlayerId}'::uuid)
                NOT VALID;
            """);

            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS global_stats;");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "GameStats"
                DROP CONSTRAINT IF EXISTS "CK_GameStats_PlayerId_NotEmpty";
            """);

            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS global_stats;");

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
    }
}
