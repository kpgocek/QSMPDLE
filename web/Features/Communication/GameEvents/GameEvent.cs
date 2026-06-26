namespace QSMPDLE.Web.Features.Communication.GameEvents;

public abstract class GameEvent
{
    public required DateTimeOffset Timestamp { get; set; }
    public required Guid PlayerId { get; set; }
    public required Guid GameId { get; set; }
}

