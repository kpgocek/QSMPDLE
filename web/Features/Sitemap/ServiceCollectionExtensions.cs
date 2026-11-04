using QSMPDLE.Web.Features.Sitemap.Services;

namespace QSMPDLE.Web.Features.Sitemap;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSitemap(this IServiceCollection services)
    {
        services.AddScoped<ISitemapService, SitemapService>();

        return services;
    }

    public static IEndpointRouteBuilder MapSitemap(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/sitemap.xml",
            async (
                HttpContext context,
                ISitemapService sitemapService,
                CancellationToken cancellationToken) =>
            {
                var xml = await sitemapService.GenerateAsync(
                    context,
                    cancellationToken);

                return Results.Text(xml, "application/xml");
            });

        return endpoints;
    }
}
