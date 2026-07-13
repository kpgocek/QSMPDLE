using Microsoft.EntityFrameworkCore;
using QSMPDLE.Web.Infrastructure.Persistence;

namespace QSMPDLE.Web.Workers;

public class StatisticsRefreshWorker(
    IDbContextFactory<ApplicationDbContext> dbContextFactory)
    : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(stoppingToken);

            await db.Database.ExecuteSqlRawAsync(
                "REFRESH MATERIALIZED VIEW global_stats;",
                stoppingToken);

            await Task.Delay(
                TimeSpan.FromMinutes(5),
                stoppingToken);
        }
    }
}
