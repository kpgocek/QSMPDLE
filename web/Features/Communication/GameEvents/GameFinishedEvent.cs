using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.Communication.GameEvents;

public sealed class GameFinishedEvent : GameEvent
{
    public required int? DayNumber { get; set; }
    public required GameMode GameMode { get; set; }
    public required int GuessCount { get; set; }
    public required bool IsWon { get; set; }
}