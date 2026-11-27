using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QSMPDLE.Web.Migrations;

/// <summary>
/// The list columns held JSON text that was parsed on every property read. Native text[] maps straight
/// to List&lt;string&gt;; the USING clauses convert existing rows, which a plain type change cannot.
/// Covered by CharacterColumnsMigrationTests.
/// </summary>
public partial class CharacterListsToTextArrays : Migration
{
    private static readonly string[] ListColumns = ["aliases", "pronouns", "affiliations", "species"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ALTER ... USING forbids subqueries, so the JSON unpacking lives in a throwaway function.
        migrationBuilder.Sql("""
            CREATE FUNCTION json_text_to_array(value text) RETURNS text[] LANGUAGE sql IMMUTABLE AS
            $$ SELECT ARRAY(SELECT jsonb_array_elements_text(COALESCE(NULLIF(value, ''), '[]')::jsonb)) $$;
            """);

        foreach (var column in ListColumns)
        {
            migrationBuilder.Sql($"""
                ALTER TABLE "Characters" ALTER COLUMN {column} TYPE text[] USING json_text_to_array({column});
                """);
        }

        migrationBuilder.Sql("DROP FUNCTION json_text_to_array(text);");

        migrationBuilder.AlterColumn<string>(
            name: "name",
            table: "Characters",
            type: "character varying(30)",
            maxLength: 30,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var column in ListColumns)
        {
            migrationBuilder.Sql($"""
                ALTER TABLE "Characters" ALTER COLUMN {column} TYPE text
                USING to_jsonb({column})::text;
                """);
        }

        migrationBuilder.AlterColumn<string>(
            name: "name",
            table: "Characters",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(30)",
            oldMaxLength: 30);
    }
}
