namespace QSMPDLE.Web.Features.Gameplay.Services;

public interface IDailyScheduleService
{
    /// <summary>Schedules missing days up to the horizon and returns how many were added.</summary>
    Task<int> EnsureScheduledAsync(CancellationToken cancellationToken = default);
}
