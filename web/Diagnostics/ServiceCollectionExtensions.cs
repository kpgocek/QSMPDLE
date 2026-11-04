using Microsoft.AspNetCore.Components.Server.Circuits;
using QSMPDLE.Web.Workers;

namespace QSMPDLE.Web.Diagnostics;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDiagnostics(this IServiceCollection services)
    {
        services.AddHostedService<MemoryDiagnosticsWorker>();
        services.AddScoped<CircuitHandler, CountingCircuitHandler>();

        return services;
    }
}
