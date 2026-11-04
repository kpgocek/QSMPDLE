using QSMPDLE.Web.Features.Archive.Models;

namespace QSMPDLE.Web.Features.Archive.Services;

public interface IArchiveStatusCache
{
    Task<Dictionary<int, DayStatus>> GetStatusesAsync(DateOnly start, DateOnly end, Func<Task<Dictionary<int, DayStatus>>> factory, CancellationToken cancellationToken = default);
    Task InvalidateAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken = default);
}
