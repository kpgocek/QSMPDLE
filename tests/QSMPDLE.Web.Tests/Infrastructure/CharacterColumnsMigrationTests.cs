using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QSMPDLE.Web.Features.Gameplay.Helpers;
using QSMPDLE.Web.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace QSMPDLE.Web.Tests.Infrastructure;

public sealed class CharacterColumnsMigrationTests : IAsyncLifetime
{
    private const string MigrationBeforeTextArrays = "20261101170507_DropGlobalStatsView";
    private const string MigrationBeforeIconGuid = "20261117150109_CharacterListsToTextArrays";
    private const string MigrationBeforePhoneNumber = "20261123231447_RepairMissingAnswers";
    private const string PortraitFolder = "343a2bcfa31a4cb79ebb12ab624d0d09";
    private const string WikiUrl = "https://qsmp.fandom.com/wiki/YoungMulti";

    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18").Build();
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;

        await using var database = new ApplicationDbContext(options);
        await database.GetService<IMigrator>().MigrateAsync(MigrationBeforeTextArrays);
        await database.Database.ExecuteSqlRawAsync($"""
            INSERT INTO "Characters" (name, aliases, pronouns, languages, affiliations, species, icon_url, page_url)
            VALUES ('YoungMulti', '["Michał Rychlik"]', '["He/Him"]', 1, '["The Federation","Polish Hussars"]', '["Human"]', '{PortraitFolder}', '{WikiUrl}'),
                   ('Wuant', '', '[]', 1, '[]', '["Unknown"]', '558b8e22624a4a1c82aca2136f8f723d', 'https://qsmp.fandom.com/wiki/Wuant');
            """);
        await database.Database.MigrateAsync();
    }

    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    [Fact]
    public async Task Migrate_JsonListColumns_ConvertToTextArrays()
    {
        await using var database = new ApplicationDbContext(options);
        var characters = await database.Characters.AsNoTracking().OrderBy(ch => ch.Name).ToListAsync();

        characters[0].Aliases.Should().BeEmpty();
        characters[0].Pronouns.Should().BeEmpty();
        characters[0].Species.Should().Equal("Unknown");
        characters[1].Aliases.Should().Equal("Michał Rychlik");
        characters[1].Pronouns.Should().Equal("He/Him");
        characters[1].Affiliations.Should().Equal("The Federation", "Polish Hussars");
        characters[1].Species.Should().Equal("Human");
    }

    [Fact]
    public async Task Migrate_IconUrlHex_ConvertsToGuidMatchingPortraitFolder()
    {
        await using var database = new ApplicationDbContext(options);
        var character = await database.Characters.AsNoTracking().SingleAsync(ch => ch.Name == "YoungMulti");

        CharacterUrlHelper.GetPortraitFolder(character.PortraitId).Should().Be(PortraitFolder);
    }

    [Fact]
    public async Task Migrate_PageUrl_KeepsOnlyWikiSlug()
    {
        await using var database = new ApplicationDbContext(options);
        var character = await database.Characters.AsNoTracking().SingleAsync(ch => ch.Name == "YoungMulti");

        character.WikiSlug.Should().Be("YoungMulti");
    }

    [Fact]
    public async Task Migrate_AddNewClues_DefaultsExistingRowsToNoPhoneNumber()
    {
        await using var database = new ApplicationDbContext(options);
        var characters = await database.Characters.AsNoTracking().ToListAsync();

        characters.Should().OnlyContain(character => character.PhoneNumber == null);
    }

    [Fact]
    public async Task Migrate_PhoneNumbers_KeepsTheFirstNumber()
    {
        await using var database = new ApplicationDbContext(options);
        await database.GetService<IMigrator>().MigrateAsync(MigrationBeforePhoneNumber);
        await database.Database.ExecuteSqlRawAsync("""UPDATE "Characters" SET phone_numbers = ARRAY[768, 317] WHERE name = 'YoungMulti';""");

        await database.Database.MigrateAsync();

        var phoneNumbers = await database.Characters.AsNoTracking().ToDictionaryAsync(ch => ch.Name, ch => ch.PhoneNumber);
        phoneNumbers["YoungMulti"].Should().Be(768);
        phoneNumbers["Wuant"].Should().BeNull();
    }

    [Fact]
    public async Task RollbackIconGuid_RestoresDashlessFolderNameAndFullUrl()
    {
        await using var database = new ApplicationDbContext(options);
        await database.GetService<IMigrator>().MigrateAsync(MigrationBeforeIconGuid);

        var iconUrl = await database.Database
            .SqlQueryRaw<string>("""SELECT icon_url AS "Value" FROM "Characters" WHERE name = 'YoungMulti'""")
            .SingleAsync();
        var pageUrl = await database.Database
            .SqlQueryRaw<string>("""SELECT page_url AS "Value" FROM "Characters" WHERE name = 'YoungMulti'""")
            .SingleAsync();

        iconUrl.Should().Be(PortraitFolder);
        pageUrl.Should().Be(WikiUrl);
    }
}
