using QSMPDLE.Web.Diagnostics;
using QSMPDLE.Web.Features.Communication.GameEvents;
using QSMPDLE.Web.Features.Gameplay.Services;

namespace QSMPDLE.Web.Services;

// Owned by one DI scope. Expiry is checked on access; the hard budget also bounds idle caches.
public sealed class ArchiveStatusCache : IArchiveStatusCache, IDisposable
{
    public const int Capacity = 4096;
    private readonly Lock gate = new();
    private readonly Dictionary<(DateOnly Start, DateOnly End), Entry> entries = [];
    private readonly TimeProvider clock;
    private readonly RuntimeCounters? counters;
    private readonly List<IDisposable> subscriptions = [];
    private int size;
    private long generation;
    private bool disposed;
    private sealed record Entry(Dictionary<int, DayStatus> Value, DateTimeOffset Expires, int Size);

    public ArchiveStatusCache(TimeProvider? clock = null, RuntimeCounters? counters = null,
        IGameEventBus? eventBus = null, IDayService? dayService = null)
    {
        this.clock = clock ?? TimeProvider.System;
        this.counters = counters;
        if (eventBus is not null && dayService is not null)
        {
            Task InvalidateDay(int? day)
            {
                if (!day.HasValue)
                    return Task.CompletedTask;
                var date = dayService.GetArchiveDate(day.Value);
                return InvalidateAsync(date, date);
            }
            subscriptions.Add(eventBus.Subscribe<GuessMadeEvent>(e => InvalidateDay(e.DayNumber)));
            subscriptions.Add(eventBus.Subscribe<GameFinishedEvent>(e => InvalidateDay(e.PuzzleId)));
        }
    }

    public async Task<Dictionary<int, DayStatus>> GetStatusesAsync(DateOnly start, DateOnly end,
        Func<Task<Dictionary<int, DayStatus>>> factory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);
        cancellationToken.ThrowIfCancellationRequested();
        long version;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            foreach (var key in entries.Where(e => e.Value.Expires <= clock.GetUtcNow()).Select(e => e.Key).ToArray())
                Remove(key);
            if (entries.TryGetValue((start, end), out var cached))
                return new(cached.Value);
            version = generation;
        }
        var created = await factory();
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            // Do not cache a fetch invalidated by a guess, or finishing after scope disposal.
            if (disposed || version != generation)
                return new(created);
            var cost = Math.Max(1, created.Count);
            if (cost > Capacity)
                return new(created);
            Remove((start, end));
            while (size + cost > Capacity)
                Remove(entries.MinBy(e => e.Value.Expires).Key);
            entries[(start, end)] = new(new(created), clock.GetUtcNow().AddMinutes(10), cost);
            size += cost;
            counters?.AddArchiveEntries(1);
        }
        return new(created);
    }

    public Task InvalidateAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            generation++;
            foreach (var key in entries.Keys.Where(k => k.Start <= end && k.End >= start).ToArray())
                Remove(key);
        }
        return Task.CompletedTask;
    }

    private void Remove((DateOnly Start, DateOnly End) key)
    {
        if (!entries.Remove(key, out var entry))
            return;
        size -= entry.Size;
        counters?.AddArchiveEntries(-1);
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
                return;
            disposed = true;
            counters?.AddArchiveEntries(-entries.Count);
            entries.Clear();
            size = 0;
        }
        foreach (var subscription in subscriptions)
            subscription.Dispose();
        subscriptions.Clear();
    }
}
