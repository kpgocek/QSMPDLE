using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QSMPDLE.Web.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Characters",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    aliases = table.Column<string>(type: "TEXT", nullable: false),
                    pronouns = table.Column<string>(type: "TEXT", nullable: false),
                    languages = table.Column<int>(type: "INTEGER", nullable: false),
                    affiliations = table.Column<string>(type: "TEXT", nullable: false),
                    species = table.Column<string>(type: "TEXT", nullable: false),
                    icon_url = table.Column<string>(type: "TEXT", nullable: false),
                    join_day_number = table.Column<int>(type: "INTEGER", nullable: true),
                    page_url = table.Column<string>(type: "TEXT", nullable: false),
                    minecraft_username = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Characters", x => x.id);
                });

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
                name: "DailyGames",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    character_id = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyGames", x => x.id);
                    table.ForeignKey(
                        name: "FK_DailyGames_Characters_character_id",
                        column: x => x.character_id,
                        principalTable: "Characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "IX_DailyGames_character_id",
                table: "DailyGames",
                column: "character_id");

            migrationBuilder.CreateIndex(
                name: "IX_DailyGames_date",
                table: "DailyGames",
                column: "date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameGuess_GameSessionId",
                table: "GameGuess",
                column: "GameSessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyGames");

            migrationBuilder.DropTable(
                name: "GameGuess");

            migrationBuilder.DropTable(
                name: "Characters");

            migrationBuilder.DropTable(
                name: "GameStats");
        }
    }
}
