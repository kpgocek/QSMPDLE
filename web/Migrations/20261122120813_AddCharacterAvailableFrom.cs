using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QSMPDLE.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterAvailableFrom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "available_from",
                table: "Characters",
                type: "date",
                nullable: true);

            // Characters from id 60 on are QSMPDLE 2.0 content and stay hidden until its release.
            migrationBuilder.Sql("""UPDATE "Characters" SET available_from = DATE '2026-12-01' WHERE id >= 60;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "available_from",
                table: "Characters");
        }
    }
}
