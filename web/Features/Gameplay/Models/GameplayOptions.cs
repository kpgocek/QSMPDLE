namespace QSMPDLE.Web.Features.Gameplay.Models;

/// <summary>Bound from the "Gameplay" configuration section.</summary>
public sealed class GameplayOptions
{
    public const string SectionName = "Gameplay";

    /// <summary>First UTC date each clue is shown; a clue without an entry is always shown.</summary>
    public Dictionary<ClueGroup, DateOnly> CluePremieres { get; init; } = [];
}
