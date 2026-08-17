namespace QSMPDLE.Web.Features.Communication.GameEvents;

public sealed class GuessMadeEvent : GameEvent
{
    public required int GuessedCharacterId { get; set; }
    // Optional day number for the game this guess applies to (archive/daily). May be null for practice.
    public int? DayNumber { get; set; }
}

