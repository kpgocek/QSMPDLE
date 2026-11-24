using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QSMPDLE.Web.Migrations
{
    /// <summary>
    /// Takes over and stops using DailyNumber and Mode. The columns stay, so the version that still runs while this
    /// one is deployed can save games; drop them (IF EXISTS) in the release after 2.0.
    /// </summary>
    public partial class DropLegacySessionColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CanonicalSessions replaced these columns; carry over any row that missed its backfill. As there, only one
            // session per player and puzzle stays active, and one that is already active wins.
            migrationBuilder.Sql("""
                WITH missed AS (
                    SELECT "Id",
                           ROW_NUMBER() OVER (
                               PARTITION BY "PlayerId", "DailyNumber"
                               ORDER BY CASE WHEN "Mode" = 0 AND "FinishedOnUtc" IS NOT NULL THEN 0 ELSE 1 END,
                                        "StartedOnUtc", "Id") AS rn
                    FROM "GameStats"
                    WHERE "PuzzleId" IS NULL AND "DailyNumber" IS NOT NULL AND "SessionCategory" = 0 AND "Mode" <> 2 AND NOT "IsLegacyDuplicate"
                )
                UPDATE "GameStats" gs
                SET "IsLegacyDuplicate" = TRUE
                FROM missed
                WHERE gs."Id" = missed."Id"
                  AND (missed.rn > 1 OR EXISTS (
                      SELECT 1 FROM "GameStats" active
                      WHERE active."PlayerId" = gs."PlayerId" AND active."PuzzleId" = gs."DailyNumber"
                        AND active."SessionCategory" = 0 AND NOT active."IsLegacyDuplicate"));

                UPDATE "GameStats"
                SET "PuzzleId" = CASE WHEN "Mode" = 2 THEN NULL ELSE "DailyNumber" END,
                    "SessionCategory" = CASE WHEN "Mode" = 2 THEN 1 ELSE 0 END,
                    "FirstEntryPoint" = "Mode"
                WHERE "PuzzleId" IS NULL AND "SessionCategory" = 0;
                """);

            // The model no longer has Mode, so new rows need a value for it.
            migrationBuilder.Sql("""ALTER TABLE "GameStats" ALTER COLUMN "Mode" SET DEFAULT 0;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Databases migrated before the columns were kept have lost them.
            migrationBuilder.Sql("""
                ALTER TABLE "GameStats"
                    ADD COLUMN IF NOT EXISTS "DailyNumber" integer,
                    ADD COLUMN IF NOT EXISTS "Mode" integer NOT NULL DEFAULT 0;
                """);

            // Mode recorded the entry point at the first guess; FirstEntryPoint is the closest surviving value.
            migrationBuilder.Sql("""
                UPDATE "GameStats"
                SET "DailyNumber" = "PuzzleId",
                    "Mode" = CASE WHEN "SessionCategory" = 1 THEN 2 WHEN "FirstEntryPoint" = 1 THEN 1 ELSE 0 END;
                """);
        }
    }
}
