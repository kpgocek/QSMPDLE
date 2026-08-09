namespace QSMPDLE.Web.Features.Sitemap.Models;

internal sealed record SitemapEntry(
    string Path,
    decimal Priority,
    string ChangeFrequency,
    DateOnly? LastModified = null);

