using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QSMPDLE.Web.Migrations;

/// <summary>
/// icon_url only ever held a portrait folder GUID, so it becomes uuid; page_url keeps just the wiki slug.
/// A plain type change cannot convert either, hence the explicit USING clauses. A page_url without the
/// wiki prefix is left whole, so an unexpected value fails the length limit instead of being truncated.
/// Down restores the dashless GUIDs (the folder names) and the full URLs. Covered by CharacterColumnsMigrationTests.
/// </summary>
public partial class CharacterIconGuidAndLengthLimits : Migration
{
    private const string WikiPrefix = "https://qsmp.fandom.com/wiki/";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "Characters" ALTER COLUMN icon_url TYPE uuid USING icon_url::uuid;
            """);

        migrationBuilder.Sql($"""
            ALTER TABLE "Characters" ALTER COLUMN page_url TYPE character varying(32)
            USING CASE WHEN starts_with(page_url, '{WikiPrefix}') THEN substr(page_url, {WikiPrefix.Length + 1}) ELSE page_url END;
            """);

        migrationBuilder.AlterColumn<string>(
            name: "name",
            table: "Characters",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(30)",
            oldMaxLength: 30);

        migrationBuilder.AlterColumn<string>(
            name: "minecraft_username",
            table: "Characters",
            type: "character varying(32)",
            maxLength: 32,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "Characters" ALTER COLUMN icon_url TYPE text USING replace(icon_url::text, '-', '');
            """);

        migrationBuilder.Sql($"""
            ALTER TABLE "Characters" ALTER COLUMN page_url TYPE text USING '{WikiPrefix}' || page_url;
            """);

        migrationBuilder.AlterColumn<string>(
            name: "name",
            table: "Characters",
            type: "character varying(30)",
            maxLength: 30,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(32)",
            oldMaxLength: 32);

        migrationBuilder.AlterColumn<string>(
            name: "minecraft_username",
            table: "Characters",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(32)",
            oldMaxLength: 32,
            oldNullable: true);
    }
}
