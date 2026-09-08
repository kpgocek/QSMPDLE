using Microsoft.EntityFrameworkCore.Migrations;

namespace QSMPDLE.Web.Infrastructure.Persistence.Migrations;

public partial class DedupeGameStats : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Remove duplicate GameStats rows per player/puzzle keeping the row with the furthest progress
        // Progress ranking: finished sessions (won/lost) > guess count; ties broken by finished/start timestamps and id.
        migrationBuilder.Sql("""
            WITH ranked AS (
              SELECT
                gs."Id",
                ROW_NUMBER() OVER (
                  PARTITION BY gs."PlayerId", gs."PuzzleId"
                  ORDER BY
                    CASE
                      WHEN gs."FinishedOnUtc" IS NOT NULL THEN 100 + CASE WHEN gs."IsWon" THEN 2 ELSE 1 END
                      ELSE (SELECT COUNT(*) FROM "GameGuess" g WHERE g."GameId" = gs."GameId")
                    END DESC,
                    gs."FinishedOnUtc" DESC NULLS LAST,
                    gs."StartedOnUtc" DESC NULLS LAST,
                    gs."Id" DESC
                ) AS rn
              FROM "GameStats" gs
              WHERE gs."PuzzleId" IS NOT NULL AND NOT gs."IsLegacyDuplicate"
            )
            DELETE FROM "GameStats"
            WHERE "Id" IN (SELECT "Id" FROM ranked WHERE rn > 1);
        """);

        // Note: Creating index concurrently is recommended on large tables, but EF migrations run inside a transaction by default.
        // Leave index creation to the normal EF index migration; this migration only ensures duplicate rows are removed first.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Nothing to undo safely – duplicates removal is destructive. Intentionally left blank.
    }
}
