namespace QSMPDLE.Web.Services
{
    public interface IArchiveStatusService
    {
        /// <summary>
        /// Returns a mapping of archive day numbers (1-based) to DayStatus for the inclusive date range.
        /// The implementation should perform a single batched query for the date range rather than one query per day.
        /// </summary>
        Task<Dictionary<int, DayStatus>> GetStatusesAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken = default);

        Task<Dictionary<int, DayStatus>> GetStatusesAsync(DateOnly start, DateOnly end, bool includeLocalStorageFallback, CancellationToken cancellationToken = default);

        Task InvalidateAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken = default);
    }
}
