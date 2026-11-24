namespace QSMPDLE.Web.Features.Gameplay.Models;

public sealed class GuessResult
{
    public CharacterLookup Character { get; init; } = null!;
    public bool IsCorrect { get; init; }

    public ComparisonResult Pronouns { get; init; }
    public ComparisonResult Languages { get; init; }
    public ComparisonResult Joined { get; init; }
    public ComparisonResult Affiliation { get; init; }
    public ComparisonResult Species { get; init; }

    /// <summary>Null when the clue had not been released on the puzzle's date.</summary>
    public ComparisonResult? QPhone { get; init; }

    /// <summary>Null when the clue had not been released on the puzzle's date.</summary>
    public ComparisonResult? Award2026 { get; init; }
}
