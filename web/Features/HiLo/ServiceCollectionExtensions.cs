using QSMPDLE.Web.Features.HiLo.Services;

namespace QSMPDLE.Web.Features.HiLo;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHiLo(this IServiceCollection services)
    {
        services.AddScoped<IHiLoService, HiLoService>();

        return services;
    }
}
