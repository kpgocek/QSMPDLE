using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Infrastructure.Persistence;
using QSMPDLE.Web.Tests.GameplayFlow;
using Testcontainers.PostgreSql;

namespace QSMPDLE.Web.Tests.Infrastructure;

public sealed class ReleaseGatingDatabaseTests : IAsyncLifetime
{
    private const string MigrationBeforeAvailableFrom = "20261118144218_AddNewClues";
    private const int UnreleasedId = 60;
    private static readonly DateOnly Release = new(2026, 12, 1);

    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18").Build();
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;

        // Characters exist before the column does, as they do in production.
        await using var database = new ApplicationDbContext(options);
        await database.GetService<IMigrator>().MigrateAsync(MigrationBeforeAvailableFrom);
        var rows = string.Join(", ", Enumerable.Range(1, 12).Append(UnreleasedId).Append(61)
            .Select(id => $"({id}, 'Character {id}', ARRAY[]::text[], ARRAY[]::text[], 1, ARRAY[]::text[], ARRAY['Human'], '{Guid.NewGuid()}', 'Character_{id}')"));
        // Every value is generated above, so the raw SQL carries no outside input.
        var insert = $"""INSERT INTO "Characters" (id, name, aliases, pronouns, language_count, affiliations, species, portrait_id, wiki_slug) VALUES {rows};""";
        await database.Database.ExecuteSqlRawAsync(insert);
        await database.Database.MigrateAsync();
    }

    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    [Fact]
    public async Task Migrate_AvailableFrom_GatesIdsFromSixtyAndLeavesExistingCharactersAvailable()
    {
        await using var database = new ApplicationDbContext(options);
        var characters = await database.Characters.AsNoTracking().ToListAsync();

        characters.Where(character => character.Id < 60).Should().OnlyContain(character => character.AvailableFrom == null);
        characters.Where(character => character.Id >= 60).Should().OnlyContain(character => character.AvailableFrom == Release);
    }

    [Fact]
    public async Task GetCharacterForDayAsync_ScheduledUnreleasedCharacter_IsReplannedAndSaved()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 11, 30, 12, 0, 0, TimeSpan.Zero));
        var dayService = new DayService(clock);
        var day = dayService.GetArchiveDayNumber(new DateOnly(2026, 11, 30));
        await ScheduleAsync(dayService, (day - 1, 1), (day, UnreleasedId), (day + 1, UnreleasedId));

        var served = await CreateStore(clock, dayService).GetCharacterForDayAsync(day);
        var servedAgain = await CreateStore(clock, dayService).GetCharacterForDayAsync(day);
        var releaseDay = await CreateStore(clock, dayService).GetCharacterForDayAsync(day + 1);

        served.Id.Should().NotBe(UnreleasedId).And.NotBe(1, "the planner keeps the repeat gap");
        served.IsAvailableOn(new DateOnly(2026, 11, 30)).Should().BeTrue();
        servedAgain.Id.Should().Be(served.Id);
        releaseDay.Id.Should().Be(UnreleasedId, "a row on or after the release date is authoritative");

        await using var database = new ApplicationDbContext(options);
        (await database.DailyGames.AsNoTracking().SingleAsync(game => game.Id == day)).CharacterId.Should().Be(served.Id);
        (await database.DailyGames.AsNoTracking().SingleAsync(game => game.Id == day + 1)).CharacterId.Should().Be(UnreleasedId);
    }

    [Fact]
    public async Task GetCharacterForDayAsync_PlayedDay_KeepsItsAnswerWhenTheReleaseDateMovesPastIt()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
        var dayService = new DayService(clock);
        var played = dayService.GetTodayDayNumber() - 1;
        await ScheduleAsync(dayService, (played, UnreleasedId));

        var served = await CreateStore(clock, dayService).GetCharacterForDayAsync(played);

        served.Id.Should().Be(UnreleasedId, "players already played that day");
        await using var database = new ApplicationDbContext(options);
        (await database.DailyGames.AsNoTracking().SingleAsync(game => game.Id == played)).CharacterId.Should().Be(UnreleasedId);
    }

    [Fact]
    public async Task GetCharacterForDayAsync_UnscheduledDaysBeforeRelease_NeverPickAnUnreleasedCharacter()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 11, 30, 12, 0, 0, TimeSpan.Zero));
        var dayService = new DayService(clock);
        var store = CreateStore(clock, dayService);
        var firstDay = dayService.GetArchiveDayNumber(new DateOnly(2026, 10, 1));
        var lastDay = dayService.GetArchiveDayNumber(Release.AddDays(-1));

        for (var day = firstDay; day <= lastDay; day++)
            (await store.GetCharacterForDayAsync(day)).Id.Should().BeLessThan(60, $"day {day} is before the release");
    }

    [Fact]
    public async Task EnsureScheduledAsync_FutureCharacters_AreOnlyScheduledFromTheirRelease()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero));
        var dayService = new DayService(clock);
        var schedule = new DailyScheduleService(new TestDbContextFactory(options), dayService, clock);

        await schedule.EnsureScheduledAsync();

        await using var database = new ApplicationDbContext(options);
        var games = await database.DailyGames.AsNoTracking().ToListAsync();
        games.Should().HaveCount(DailyScheduleService.DaysAhead);
        games.Where(game => game.Date < Release).Should().OnlyContain(game => game.CharacterId < 60);
        games.Where(game => game.Date >= Release).Should().Contain(game => game.CharacterId >= 60);
    }

    [Fact]
    public async Task GetLookupsAvailableOnAsync_WarmCacheCrossesReleaseMidnight_ShowsNewCharactersOnlyForNewDates()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 11, 30, 23, 59, 0, TimeSpan.Zero));
        var dayService = new DayService(clock);
        var availability = new GameplayAvailabilityService(dayService, Microsoft.Extensions.Options.Options.Create(new GameplayOptions()));
        var store = CreateStore(clock, dayService);
        var archiveDay = dayService.GetTodayDayNumber();

        var before = await store.GetLookupsAvailableOnAsync(availability.GetEffectiveDate(null));
        clock.Now = clock.Now.AddMinutes(2);
        var today = await store.GetLookupsAvailableOnAsync(availability.GetEffectiveDate(null));
        var archive = await store.GetLookupsAvailableOnAsync(availability.GetEffectiveDate(archiveDay));

        before.Select(lookup => lookup.Id).Should().NotContain(UnreleasedId);
        today.Select(lookup => lookup.Id).Should().Contain(UnreleasedId);
        archive.Select(lookup => lookup.Id).Should().NotContain(UnreleasedId);
        (await store.GetLookupsAsync()).Select(lookup => lookup.Id).Should().Contain(UnreleasedId, "recorded ids still resolve to names");
    }

    private CachedDatabaseCharacterStore CreateStore(TimeProvider clock, IDayService dayService) =>
        new(new TestDbContextFactory(options), new MemoryCache(new MemoryCacheOptions()), dayService, clock, NullLogger<CachedDatabaseCharacterStore>.Instance);

    private async Task ScheduleAsync(IDayService dayService, params (int Day, int CharacterId)[] rows)
    {
        await using var database = new ApplicationDbContext(options);
        database.DailyGames.AddRange(rows.Select(row => new DailyGame { Id = row.Day, Date = dayService.GetArchiveDate(row.Day), CharacterId = row.CharacterId }));
        await database.SaveChangesAsync();
    }

    private sealed class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);
    }
}
