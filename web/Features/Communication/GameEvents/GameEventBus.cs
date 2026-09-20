using QSMPDLE.Web.Diagnostics;
namespace QSMPDLE.Web.Features.Communication.GameEvents;

public sealed class GameEventBus(ILogger<GameEventBus> logger, RuntimeCounters counters) : IGameEventBus, IDisposable
{
    private readonly Lock gate = new();
    private readonly List<Subscription> subscriptions = [];
    private bool disposed;

    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : class
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var subscription = new Subscription(this, typeof(TEvent), value => handler((TEvent)value));
            subscriptions.Add(subscription);
            counters.AddSubscriptions(1);
            return subscription;
        }
    }

    public async Task PublishAsync<TEvent>(TEvent eventData) where TEvent : class
    {
        Subscription[] snapshot;
        lock (gate)
            snapshot = subscriptions.Where(s => s.EventType == typeof(TEvent)).ToArray();
        foreach (var subscription in snapshot)
        {
            var handler = subscription.Handler;
            if (handler is null) continue;
            try { await handler(eventData); }
            catch (Exception exception)
            {
                // UI notifications must not abort persistence or another subscriber.
                logger.LogError(exception, "Game notification failed for {EventType}", typeof(TEvent).Name);
            }
        }
    }

    private void Remove(Subscription subscription)
    {
        lock (gate)
        {
            subscription.Clear();
            if (subscriptions.Remove(subscription)) counters.AddSubscriptions(-1);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            foreach (var subscription in subscriptions) subscription.Clear();
            counters.AddSubscriptions(-subscriptions.Count);
            subscriptions.Clear();
        }
    }

    private sealed class Subscription(GameEventBus owner, Type eventType, Func<object, Task> handler) : IDisposable
    {
        private Func<object, Task>? callback = handler;
        public Type EventType { get; } = eventType;
        public Func<object, Task>? Handler => Volatile.Read(ref callback);
        public void Clear() => Interlocked.Exchange(ref callback, null);
        public void Dispose() => owner.Remove(this);
    }
}
