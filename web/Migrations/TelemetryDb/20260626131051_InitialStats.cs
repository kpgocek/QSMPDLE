using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QSMPDLE.Web.Migrations.TelemetryDb
{
    /// <inheritdoc />
    public partial class InitialStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    DailyNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetCharacterId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedOnUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    FinishedOnUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsWon = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameStats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameGuess",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    GuessOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    GuessedCharacterId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameGuess", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameGuess_GameStats_GameSessionId",
                        column: x => x.GameSessionId,
                        principalTable: "GameStats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameGuess_GameId",
                table: "GameGuess",
                column: "GameId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameGuess_GameSessionId",
                table: "GameGuess",
                column: "GameSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_GameStats_GameId",
                table: "GameStats",
                column: "GameId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameGuess");

            migrationBuilder.DropTable(
                name: "GameStats");
        }
    }
}
