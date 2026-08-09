using System.Globalization;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QSMPDLE.Web.Features.Sitemap.Models;
using QSMPDLE.Web.Infrastructure.Persistence;

namespace QSMPDLE.Web.Features.Sitemap.Services;

public sealed class SitemapService(
    ApplicationDbContext db,
    IMemoryCache cache)
    : ISitemapService
{
    private const string CacheKey = "sitemap";

    public async Task<string> GenerateAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        return await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);

            return await GenerateInternalAsync(httpContext, cancellationToken);
        }) ?? string.Empty;
    }

    private async Task<string> GenerateInternalAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";

        var archiveGames = await db.DailyGames
            .AsNoTracking()
            .OrderBy(g => g.Id)
            .Select(g => new
            {
                g.Id,
                g.Date
            })
            .ToListAsync(cancellationToken);

        var latestArchiveDate = archiveGames
            .Select(g => (DateOnly?)g.Date)
            .Max();

        var staticPages = BuildStaticPages(latestArchiveDate);

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

        var root = new XElement(ns + "urlset");

        foreach (var page in staticPages)
        {
            root.Add(CreateUrl(ns, baseUrl, page));
        }

        foreach (var game in archiveGames)
        {
            root.Add(CreateUrl(
                ns,
                baseUrl,
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

    private static IReadOnlyList<SitemapEntry> BuildStaticPages(DateOnly? latestArchiveDate)
    {
        return
        [
            new("/", 1.0m, "daily", latestArchiveDate),
            new("/archive", 0.9m, "daily", latestArchiveDate),
            new("/practice", 0.9m, "weekly"),
            new("/about", 0.7m, "monthly"),
            new("/privacy", 0.3m, "yearly")
        ];
    }

    private static XElement CreateUrl(
        XNamespace ns,
        string baseUrl,
        SitemapEntry entry)
    {
        var url = new XElement(ns + "url",
            new XElement(ns + "loc", $"{baseUrl}{entry.Path}"));

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