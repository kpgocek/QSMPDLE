using QSMPDLE.Web.Features.Archive.Services;

namespace QSMPDLE.Web.Features.Archive;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddArchive(this IServiceCollection services)
    {
        services.AddScoped<IArchiveStatusService, ArchiveStatusService>();
        services.AddScoped<IArchiveGameStateSource, ArchiveGameStateSource>();

        return services;
    }
}
