namespace QSMPDLE.Web.Features.Sitemap.Services;

public interface ISitemapService
{
    Task<string> GenerateAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
}

