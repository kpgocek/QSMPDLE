namespace QSMPDLE.Web.Features.Communication.GameEvents;

public class GameEventBus : IGameEventBus
{
    private readonly Dictionary<Type, List<Func<object, Task>>> handlers = [];

    public void Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : class
    {
        var eventType = typeof(TEvent);

        // New list for the event type if it doesn't exist
        if (!handlers.TryGetValue(eventType, out var list))
        {
            list = [];
            handlers[eventType] = list;
        }

        list.Add(x => handler((TEvent)x));
    }

    public async Task PublishAsync<TEvent>(TEvent eventData) where TEvent : class
    {
        if (!handlers.TryGetValue(typeof(TEvent), out var list))
        {
            return;
        }

        foreach (var handler in list)
        {
            await handler(eventData);
        }
    }
}

