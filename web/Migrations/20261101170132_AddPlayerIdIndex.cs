using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QSMPDLE.Web.Migrations;

/// <summary>Serves per-player history and archive queries, which the canonical partial index cannot.</summary>
public partial class AddPlayerIdIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_GameStats_PlayerId",
            table: "GameStats",
            column: "PlayerId",
            filter: "NOT \"IsLegacyDuplicate\"");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_GameStats_PlayerId",
            table: "GameStats");
    }
}
