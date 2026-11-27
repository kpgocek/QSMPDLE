using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QSMPDLE.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddHiLo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "phone_number",
                table: "Characters",
                type: "integer",
                nullable: true);

            // The first number is the personal one; the second numbers are lost and Down cannot restore them.
            migrationBuilder.Sql("""UPDATE "Characters" SET phone_number = phone_numbers[1];""");

            migrationBuilder.DropColumn(
                name: "phone_numbers",
                table: "Characters");

            migrationBuilder.CreateTable(
                name: "HiLoScores",
                columns: table => new
                {
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BestScore = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HiLoScores", x => x.PlayerId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HiLoScores");

            migrationBuilder.AddColumn<List<int>>(
                name: "phone_numbers",
                table: "Characters",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.Sql("""UPDATE "Characters" SET phone_numbers = ARRAY[phone_number] WHERE phone_number IS NOT NULL;""");

            migrationBuilder.DropColumn(
                name: "phone_number",
                table: "Characters");
        }
    }
}
