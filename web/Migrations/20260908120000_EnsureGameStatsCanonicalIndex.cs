using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using QSMPDLE.Web.Infrastructure.Persistence;

#nullable disable

namespace QSMPDLE.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260908120000_EnsureGameStatsCanonicalIndex")]
public partial class EnsureGameStatsCanonicalIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Fail early if canonical duplicates exist so the deployment doesn't
        // attempt to create a unique index that would fail or crash the DB.
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM (
                        SELECT "PlayerId", "PuzzleId"
                        FROM "GameStats"
                        WHERE "PuzzleId" IS NOT NULL AND NOT "IsLegacyDuplicate"
                        GROUP BY "PlayerId", "PuzzleId"
                        HAVING COUNT(*) > 1
                    ) t
                ) THEN
                    RAISE EXCEPTION 'Duplicate canonical GameStats exist. Clean duplicates and run scripts/create_game_stats_index.sql manually (outside transaction).';
                END IF;
            END
            $$;
            """);

        // Recreate the filtered unique index in-transaction. Non-concurrent
        // creation may acquire locks on very large tables; if that is
        // unacceptable, run the provided scripts/create_game_stats_index.sql
        // manually (outside a transaction) after cleaning duplicates.
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS "IX_GameStats_PlayerId_PuzzleId_ActiveCanonical";

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_GameStats_PlayerId_PuzzleId_ActiveCanonical"
            ON "GameStats" ("PlayerId", "PuzzleId", "ActiveCanonical")
            WHERE "PuzzleId" IS NOT NULL AND NOT "IsLegacyDuplicate";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_GameStats_PlayerId_PuzzleId_ActiveCanonical\";");
    }
}
