using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QSMPDLE.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddNewClues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "kick_followers",
                table: "Characters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<List<int>>(
                name: "phone_numbers",
                table: "Characters",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<int>(
                name: "twitch_followers",
                table: "Characters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "won_award_2026",
                table: "Characters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "youtube_subscribers",
                table: "Characters",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "kick_followers",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "phone_numbers",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "twitch_followers",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "won_award_2026",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "youtube_subscribers",
                table: "Characters");
        }
    }
}
