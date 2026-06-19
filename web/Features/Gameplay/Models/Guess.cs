using QSMPDLE.Web.Models;

namespace QSMPDLE.Web.Features.Gameplay.Models;

public sealed class Guess
{
    public Character Member { get; init; } = null!;
    public bool IsCorrect { get; init; }

    public ComparisonResult Pronouns { get; init; }
    public ComparisonResult Languages { get; init; }
    public ComparisonResult Joined { get; init; }
    public ComparisonResult Affiliation { get; init; }
    public ComparisonResult Species { get; init; }
}
