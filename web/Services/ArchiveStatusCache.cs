using Microsoft.Extensions.Caching.Memory;

namespace QSMPDLE.Web.Services;

public sealed class ArchiveStatusCache(IMemoryCache memoryCache) : IArchiveStatusCache
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public async Task<Dictionary<int, DayStatus>> GetStatusesAsync(DateOnly start, DateOnly end, Func<Task<Dictionary<int, DayStatus>>> factory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var cacheKey = GetCacheKey(start, end);
        if (memoryCache.TryGetValue(cacheKey, out Dictionary<int, DayStatus>? cached) && cached is not null)
        {
            return new Dictionary<int, DayStatus>(cached);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var created = await factory();
        memoryCache.Set(cacheKey, new Dictionary<int, DayStatus>(created), CacheDuration);
        return created;
    }

    public Task InvalidateAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        memoryCache.Remove(GetCacheKey(start, end));
        return Task.CompletedTask;
    }

    private static string GetCacheKey(DateOnly start, DateOnly end) => $"archive-status:{start.DayNumber}:{end.DayNumber}";
}
