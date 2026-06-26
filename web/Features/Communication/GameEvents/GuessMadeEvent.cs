namespace QSMPDLE.Web.Features.Communication.GameEvents;

public sealed class GuessMadeEvent : GameEvent
{
    public required int GuessedCharacterId { get; set; }
}

