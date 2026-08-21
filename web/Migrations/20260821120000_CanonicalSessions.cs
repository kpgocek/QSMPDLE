using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using QSMPDLE.Web.Infrastructure.Persistence;

#nullable disable

namespace QSMPDLE.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260821120000_CanonicalSessions")]
public partial class CanonicalSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "PuzzleId", table: "GameStats", type: "integer", nullable: true);
        migrationBuilder.AddColumn<int>(name: "SessionCategory", table: "GameStats", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>(name: "FirstEntryPoint", table: "GameStats", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<bool>(name: "IsLegacyDuplicate", table: "GameStats", type: "boolean", nullable: false, defaultValue: false);

        migrationBuilder.Sql("""
            UPDATE "GameStats"
            SET "PuzzleId" = "DailyNumber",
                "SessionCategory" = CASE WHEN "Mode" = 2 THEN 1 ELSE 0 END,
                "FirstEntryPoint" = "Mode";
            """);

        // Historical Daily/Archive duplicates remain available for audit but are
        // excluded from the canonical result set. A completed Daily takes precedence.
        migrationBuilder.Sql("""
            WITH ranked AS (
                SELECT "Id",
                       ROW_NUMBER() OVER (
                           PARTITION BY "PlayerId", "PuzzleId"
                           ORDER BY CASE WHEN "Mode" = 0 AND "FinishedOnUtc" IS NOT NULL THEN 0 ELSE 1 END,
                                    "StartedOnUtc", "Id") AS rn
                FROM "GameStats"
                WHERE "SessionCategory" = 0 AND "PuzzleId" IS NOT NULL
            )
            UPDATE "GameStats" gs
            SET "IsLegacyDuplicate" = TRUE
            FROM ranked
            WHERE gs."Id" = ranked."Id" AND ranked.rn > 1;
            """);

        // The old schema allowed duplicate GameId values. Repair them before
        // enforcing the identifier invariant and keep GameGuess in sync.
        migrationBuilder.Sql("""
            WITH ranked AS (
                SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "GameId" ORDER BY "Id") AS rn
                FROM "GameStats"
            )
            UPDATE "GameStats" gs
            SET "GameId" = md5(random()::text || clock_timestamp()::text || gs."Id"::text)::uuid
            FROM ranked
            WHERE gs."Id" = ranked."Id" AND ranked.rn > 1;

            UPDATE "GameGuess" gg
            SET "GameId" = gs."GameId"
            FROM "GameStats" gs
            WHERE gg."GameSessionId" = gs."Id";
            """);

        migrationBuilder.DropIndex(name: "IX_GameStats_GameId", table: "GameStats");
        migrationBuilder.CreateIndex(name: "IX_GameStats_GameId", table: "GameStats", column: "GameId", unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_GameStats_PlayerId_PuzzleId_ActiveCanonical",
            table: "GameStats",
            columns: new[] { "PlayerId", "PuzzleId" },
            unique: true,
            filter: "\"PuzzleId\" IS NOT NULL AND NOT \"IsLegacyDuplicate\"");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_GameStats_PlayerId_PuzzleId_ActiveCanonical", table: "GameStats");
        migrationBuilder.DropIndex(name: "IX_GameStats_GameId", table: "GameStats");
        migrationBuilder.CreateIndex(name: "IX_GameStats_GameId", table: "GameStats", column: "GameId");
        migrationBuilder.DropColumn(name: "PuzzleId", table: "GameStats");
        migrationBuilder.DropColumn(name: "SessionCategory", table: "GameStats");
        migrationBuilder.DropColumn(name: "FirstEntryPoint", table: "GameStats");
        migrationBuilder.DropColumn(name: "IsLegacyDuplicate", table: "GameStats");
    }
}
