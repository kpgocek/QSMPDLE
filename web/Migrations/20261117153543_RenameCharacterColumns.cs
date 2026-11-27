using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QSMPDLE.Web.Migrations
{
    /// <inheritdoc />
    public partial class RenameCharacterColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "page_url",
                table: "Characters",
                newName: "wiki_slug");

            migrationBuilder.RenameColumn(
                name: "languages",
                table: "Characters",
                newName: "language_count");

            migrationBuilder.RenameColumn(
                name: "icon_url",
                table: "Characters",
                newName: "portrait_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "wiki_slug",
                table: "Characters",
                newName: "page_url");

            migrationBuilder.RenameColumn(
                name: "portrait_id",
                table: "Characters",
                newName: "icon_url");

            migrationBuilder.RenameColumn(
                name: "language_count",
                table: "Characters",
                newName: "languages");
        }
    }
}
