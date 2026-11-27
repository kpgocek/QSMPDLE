namespace QSMPDLE.Web.Features.Gameplay.Models;

public sealed class Game
{
    /// <summary>Identifier of a scheduled puzzle. Null only for practice games.</summary>
    public int? PuzzleId { get; set; }

    public required int TargetId { get; set; }
    public required string PortraitUrl { get; set; }
}
