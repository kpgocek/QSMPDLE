using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using QSMPDLE.Web.Diagnostics;
using QSMPDLE.Web.Features.Communication;
using QSMPDLE.Web.Features.Communication.GameEvents;
using QSMPDLE.Web.Services;

namespace QSMPDLE.Web.Tests.Services;

public sealed class RuntimeLifetimeTests
{
    [Fact]
    public async Task EventScopesAreIsolatedAndDisposedSubscriptionsAreReleased()
    {
        var services = new ServiceCollection().AddLogging().AddInternalCommunication();
        await using var provider = services.BuildServiceProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var bus = first.ServiceProvider.GetRequiredService<IGameEventBus>();
        var other = second.ServiceProvider.GetRequiredService<IGameEventBus>();
        var counters = provider.GetRequiredService<RuntimeCounters>();
        var calls = 0;
        var subscription = bus.Subscribe<string>(_ => { calls++; return Task.CompletedTask; });
        await other.PublishAsync("test");
        Assert.Equal(0, calls);
        await bus.PublishAsync("test");
        Assert.Equal(1, calls);
        subscription.Dispose();
        subscription.Dispose();
        await bus.PublishAsync("test");
        Assert.Equal(1, calls);
        Assert.Equal(0, counters.Subscriptions);
        bus.Subscribe<string>(_ => Task.CompletedTask);
        first.Dispose();
        Assert.Equal(0, counters.Subscriptions);
    }

    [Fact]
    public async Task DisposedSubscriberInSnapshotIsSkippedAndFailuresDoNotAbortPublication()
    {
        var counters = new RuntimeCounters();
        using var bus = new GameEventBus(NullLogger<GameEventBus>.Instance, counters);
        IDisposable? removed = null;
        bus.Subscribe<string>(_ => { removed!.Dispose(); throw new InvalidOperationException("stale UI"); });
        var removedCalls = 0;
        removed = bus.Subscribe<string>(_ => { removedCalls++; return Task.CompletedTask; });
        var calls = 0;
        bus.Subscribe<string>(_ => { calls++; return Task.CompletedTask; });
        await bus.PublishAsync("test");
        Assert.Equal(1, calls);
        Assert.Equal(2, counters.Subscriptions);
        Assert.Equal(0, removedCalls);
    }

    [Fact]
    public async Task CircuitCountsHandleReconnectAndRepeatedClose()
    {
        var counters = new RuntimeCounters();
        using var handler = new CountingCircuitHandler(counters);
        await handler.OnCircuitOpenedAsync(null!, default);
        await handler.OnConnectionUpAsync(null!, default);
        await handler.OnConnectionUpAsync(null!, default);
        Assert.Equal(1, counters.Open);
        Assert.Equal(1, counters.Connected);
        await handler.OnConnectionDownAsync(null!, default);
        Assert.Equal(1, counters.Disconnected);
        await handler.OnConnectionUpAsync(null!, default);
        Assert.Equal(0, counters.Disconnected);
        await handler.OnCircuitClosedAsync(null!, default);
        handler.Dispose();
        Assert.Equal(0, counters.Open);
        Assert.Equal(0, counters.Connected);
    }

    [Fact]
    public async Task ArchiveCachesAreIsolatedCopyValuesExpireAndInvalidateOverlappingRanges()
    {
        var clock = new TestClock();
        var counters = new RuntimeCounters();
        using var first = new ArchiveStatusCache(clock, counters);
        using var second = new ArchiveStatusCache(clock, counters);
        var date = new DateOnly(2026, 6, 15);
        var calls = 0;
        Task<Dictionary<int, DayStatus>> Load()
        {
            calls++;
            return Task.FromResult(new Dictionary<int, DayStatus> { [date.DayNumber] = DayStatus.Won });
        }
        var value = await first.GetStatusesAsync(date, date.AddDays(30), Load);
        value.Clear();
        Assert.Single(await first.GetStatusesAsync(date, date.AddDays(30), Load));
        Assert.Equal(1, calls);
        await second.GetStatusesAsync(date, date.AddDays(30), Load);
        Assert.Equal(2, calls);
        await first.InvalidateAsync(date.AddDays(1), date.AddDays(1));
        await first.GetStatusesAsync(date, date.AddDays(30), Load);
        Assert.Equal(3, calls);
        clock.Now = clock.Now.AddMinutes(11);
        await first.GetStatusesAsync(date, date.AddDays(30), Load);
        Assert.Equal(4, calls);
        first.Dispose();
        second.Dispose();
        Assert.Equal(0, counters.ArchiveEntries);
    }

    [Fact]
    public async Task ArchiveCacheIsBoundedAndDoesNotCacheInvalidatedInflightFetch()
    {
        var counters = new RuntimeCounters();
        using var cache = new ArchiveStatusCache(counters: counters);
        var date = new DateOnly(2026, 6, 15);
        var full = Enumerable.Range(0, ArchiveStatusCache.Capacity).ToDictionary(i => i, _ => DayStatus.Won);
        await cache.GetStatusesAsync(date, date, () => Task.FromResult(full));
        await cache.GetStatusesAsync(date.AddDays(1), date.AddDays(1), () => Task.FromResult(full));
        Assert.Equal(1, counters.ArchiveEntries);
        var called = false;
        await cache.GetStatusesAsync(date, date, () => { called = true; return Task.FromResult(full); });
        Assert.True(called);
        var pending = new TaskCompletionSource<Dictionary<int, DayStatus>>();
        var fetch = cache.GetStatusesAsync(date.AddDays(2), date.AddDays(2), () => pending.Task);
        await cache.InvalidateAsync(date.AddDays(2), date.AddDays(2));
        pending.SetResult(new() { [1] = DayStatus.Won });
        await fetch;
        Assert.Equal(1, counters.ArchiveEntries);
    }

    [Fact]
    public async Task GuessInvalidatesCachedMonthAndScopeDisposalReleasesCacheSubscriptions()
    {
        var counters = new RuntimeCounters();
        using var bus = new GameEventBus(NullLogger<GameEventBus>.Instance, counters);
        var days = new QSMPDLE.Web.Features.Gameplay.Services.DayService();
        using var cache = new ArchiveStatusCache(counters: counters, eventBus: bus, dayService: days);
        var start = days.GetFirstDay();
        await cache.GetStatusesAsync(start, start.AddDays(29),
            () => Task.FromResult(new Dictionary<int, DayStatus> { [start.DayNumber] = DayStatus.NotStarted }));
        await bus.PublishAsync(new GuessMadeEvent
        {
            Timestamp = DateTimeOffset.UtcNow, PlayerId = Guid.NewGuid(), GameId = Guid.NewGuid(),
            GuessedCharacterId = 1, DayNumber = 1
        });
        Assert.Equal(0, counters.ArchiveEntries);
        Assert.Equal(2, counters.Subscriptions);
        cache.Dispose();
        Assert.Equal(0, counters.Subscriptions);
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 6, 15, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
