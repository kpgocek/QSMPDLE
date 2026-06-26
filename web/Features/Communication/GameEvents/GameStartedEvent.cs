using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.Communication.GameEvents;

public sealed class GameStartedEvent : GameEvent
{
    public required GameMode GameMode { get; set; }
    public required int TargetCharacterId { get; set; }
    public int? DayNumber { get; set; }
}

