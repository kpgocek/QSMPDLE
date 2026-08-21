using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.Communication.GameEvents;

public sealed class GameFinishedEvent : GameEvent
{
    public SessionCategory SessionCategory { get; set; }
    public EntryPoint EntryPoint { get; set; }
    public int? PuzzleId { get; set; }

    public int? DayNumber { get; set; }
    public GameMode GameMode { get; set; }
    public required int GuessCount { get; set; }
    public required bool IsWon { get; set; }
}
