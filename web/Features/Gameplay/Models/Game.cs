namespace QSMPDLE.Web.Features.Gameplay.Models;

public sealed class Game
{
    /// <summary>Identifier of a scheduled puzzle. Null only for practice games.</summary>
    public int? PuzzleId { get; set; }

    // Reads browser states written before PuzzleId was introduced.
    [System.Text.Json.Serialization.JsonPropertyName("dayNumber")]
    public int? LegacyDayNumber
    {
        get => PuzzleId;
        set => PuzzleId = value;
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public int? DayNumber
    {
        get => PuzzleId;
        set => PuzzleId = value;
    }
    public required int TargetId { get; set; }
    public required string PortraitUrl { get; set; }
}
