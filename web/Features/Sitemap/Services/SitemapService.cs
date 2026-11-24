using System.Globalization;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QSMPDLE.Web.Extensions;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Features.Sitemap.Models;
using QSMPDLE.Web.Infrastructure.Persistence;

namespace QSMPDLE.Web.Features.Sitemap.Services;

/// <summary>
/// Lists only archive days a visitor can already open: the schedule runs weeks ahead, and those rows
/// must not reach crawlers. The cached copy expires at the next UTC midnight, when another day opens.
/// </summary>
public sealed class SitemapService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IMemoryCache cache,
    IDayService dayService,
    TimeProvider timeProvider)
    : ISitemapService
{
    private const string CacheKey = "sitemap";

    public async Task<string> GenerateAsync(CancellationToken cancellationToken = default)
    {
        return await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpiration = new DateTimeOffset(timeProvider.GetUtcNow().UtcDateTime.Date.AddDays(1), TimeSpan.Zero);

            return await GenerateInternalAsync(cancellationToken);
        }) ?? string.Empty;
    }

    private async Task<string> GenerateInternalAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var maxArchiveDay = dayService.GetMaxArchiveDay();
        var today = dayService.GetArchiveDate(dayService.GetTodayDayNumber());

        var archiveGames = await db.DailyGames
            .AsNoTracking()
            .Where(g => g.Id <= maxArchiveDay)
            .OrderBy(g => g.Id)
            .Select(g => new
            {
                g.Id,
                g.Date
            })
            .ToListAsync(cancellationToken);

        var latestArchiveDate = archiveGames
            .Max(g => (DateOnly?)g.Date);

        var staticPages = BuildStaticPages(today, latestArchiveDate);

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

        var root = new XElement(ns + "urlset");

        foreach (var page in staticPages)
        {
            root.Add(CreateUrl(ns, page));
        }

        foreach (var game in archiveGames)
        {
            root.Add(CreateUrl(
                ns,
                new SitemapEntry(
                    $"/archive/day/{game.Id}",
                    0.8m,
                    "never",
                    game.Date)));
        }

        var document = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            root);

        return document.ToString();
    }

    private static IReadOnlyList<SitemapEntry> BuildStaticPages(DateOnly today, DateOnly? latestArchiveDate)
    {
        return
        [
            new("/", 1.0m, "daily", today),
            new("/archive", 0.9m, "daily", latestArchiveDate),
            new("/practice", 0.9m, "weekly"),
            new("/how-to-play", 0.7m, "monthly"),
            new("/about", 0.7m, "monthly"),
            new("/privacy", 0.3m, "yearly")
        ];
    }

    private static XElement CreateUrl(
        XNamespace ns,
        SitemapEntry entry)
    {
        var url = new XElement(ns + "url",
            new XElement(ns + "loc", $"{Site.Url}{entry.Path}"));

        if (entry.LastModified is not null)
        {
            url.Add(new XElement(
                ns + "lastmod",
                entry.LastModified.Value.ToString("yyyy-MM-dd")));
        }

        url.Add(
            new XElement(ns + "changefreq", entry.ChangeFrequency),
            new XElement(
                ns + "priority",
                entry.Priority.ToString("0.0", CultureInfo.InvariantCulture)));

        return url;
    }
}
