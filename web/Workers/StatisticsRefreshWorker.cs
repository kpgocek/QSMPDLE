using Microsoft.EntityFrameworkCore;
using QSMPDLE.Web.Infrastructure.Persistence;

namespace QSMPDLE.Web.Workers;

public class StatisticsRefreshWorker(
    IServiceProvider services)
    : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = services.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            await db.Database.ExecuteSqlRawAsync(
                "REFRESH MATERIALIZED VIEW global_stats;",
                stoppingToken);

            await Task.Delay(
                TimeSpan.FromMinutes(5),
                stoppingToken);
        }
    }
}