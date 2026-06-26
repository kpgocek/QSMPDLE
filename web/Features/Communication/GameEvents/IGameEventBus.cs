namespace QSMPDLE.Web.Features.Communication.GameEvents;

public interface IGameEventBus
{
    void Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : class;
    Task PublishAsync<TEvent>(TEvent eventData) where TEvent : class;
}
