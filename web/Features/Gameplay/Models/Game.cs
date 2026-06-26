namespace QSMPDLE.Web.Features.Gameplay.Models;

public sealed class Game
{
    public int? DayNumber { get; set; }
    public required int TargetId { get; set; }
    public required string PortraitUrl { get; set; }
}
