using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QSMPDLE.Web.Migrations
{
    /// <summary>Old sessions without an answer could not be opened; the schedule knows it.</summary>
    public partial class RepairMissingAnswers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A saved result that the scheduled answer contradicts keeps its unknown answer.
            migrationBuilder.Sql("""
                UPDATE "GameStats" gs
                SET "TargetCharacterId" = d.character_id
                FROM "DailyGames" d
                WHERE gs."TargetCharacterId" = 0 AND gs."SessionCategory" = 0 AND gs."PuzzleId" = d.id
                  AND (gs."FinishedOnUtc" IS NULL OR gs."IsWon" = EXISTS (
                      SELECT 1 FROM "GameGuess" g
                      WHERE g."GameSessionId" = gs."Id" AND g."GuessedCharacterId" = d.character_id));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The repaired answers stay: they are correct, and nothing marks which rows had none.
        }
    }
}
