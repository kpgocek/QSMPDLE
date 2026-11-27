namespace QSMPDLE.Web.Features.Gameplay.Services;

/// <summary>
///     Tops up the daily schedule at startup and twice a day, so it never runs out while the app is
///     deployed. A failed run is harmless: unscheduled days fall back to a deterministic pick.
/// </summary>
public sealed class DailyScheduleWorker(
    IDailyScheduleService schedule,
    TimeProvider timeProvider,
    ILogger<DailyScheduleWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(12);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        try
        {
            do
            {
                await TopUpAsync(stoppingToken);
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task TopUpAsync(CancellationToken cancellationToken)
    {
        try
        {
            var added = await schedule.EnsureScheduledAsync(cancellationToken);
            if (added > 0)
            {
                logger.LogInformation("Scheduled {Count} upcoming daily games.", added);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Daily schedule top-up failed; it retries on the next run.");
        }
    }
}
