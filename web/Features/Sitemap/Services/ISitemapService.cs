namespace QSMPDLE.Web.Features.Sitemap.Services;

public interface ISitemapService
{
    Task<string> GenerateAsync(CancellationToken cancellationToken = default);
}
