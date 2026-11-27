using System.Xml.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Features.Sitemap.Services;
using QSMPDLE.Web.Infrastructure.Persistence;
using QSMPDLE.Web.Tests.GameplayFlow;
using Testcontainers.PostgreSql;

namespace QSMPDLE.Web.Tests.Infrastructure;

public sealed class SitemapServiceDatabaseTests : IAsyncLifetime
{
    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18").Build();
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;

        await using var database = new ApplicationDbContext(options);
        await database.Database.MigrateAsync();
        database.Characters.Add(new Character { Id = 1, Name = "Target", WikiSlug = "Target" });
        await database.SaveChangesAsync();
    }

    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    [Fact]
    public async Task GenerateAsync_ScheduleRunsAhead_ListsOnlyOpenableArchiveDays()
    {
        // Arrange: today is day 10; days 1-9 are playable archive days, 10 is today's daily, 11-20 are scheduled ahead.
        var clock = new TestClock(new DateTimeOffset(2026, 6, 24, 15, 0, 0, TimeSpan.Zero));
        var dayService = new DayService(clock);
        await using (var database = new ApplicationDbContext(options))
        {
            database.DailyGames.AddRange(Enumerable.Range(1, 20).Select(day => new DailyGame
            {
                Id = day,
                Date = dayService.GetArchiveDate(day),
                CharacterId = 1,
            }));
            await database.SaveChangesAsync();
        }

        var service = new SitemapService(new TestDbContextFactory(options), new MemoryCache(new MemoryCacheOptions()), dayService, clock);

        // Act
        var urls = XDocument.Parse(await service.GenerateAsync()).Root!.Elements(Ns + "url")
            .ToDictionary(url => url.Element(Ns + "loc")!.Value, url => url.Element(Ns + "lastmod")?.Value);

        // Assert
        urls.Keys.Where(loc => loc.Contains("/archive/day/", StringComparison.Ordinal))
            .Should().BeEquivalentTo(Enumerable.Range(1, 9).Select(day => $"https://qsmpdle.com/archive/day/{day}"));
        urls["https://qsmpdle.com/"].Should().Be("2026-06-24");
        urls["https://qsmpdle.com/archive"].Should().Be("2026-06-23");
    }

    private sealed class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);
    }
}
