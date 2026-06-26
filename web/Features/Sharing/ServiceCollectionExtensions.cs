using QSMPDLE.Web.Features.Sharing.Builders;
using QSMPDLE.Web.Features.Sharing.Services;

namespace QSMPDLE.Web.Features.Sharing;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSharingFeature(this IServiceCollection services)
    {
        services.AddScoped<IShareService, ShareService>();
        services.AddSingleton<IShareTextBuilder, ShareTextBuilder>();

        return services;
    }
}
