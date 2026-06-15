using QSMPDLE.Web.Models;

namespace QSMPDLE.Web.Data.Gameplay;

public sealed class Guess
{
    public Member Member { get; init; } = null!;
    public bool IsCorrect { get; init; }

    public ComparisonResult Pronouns { get; init; }
    public ComparisonResult Languages { get; init; }
    public ComparisonResult Joined { get; init; }
    public ComparisonResult Affiliation { get; init; }
    public ComparisonResult Species { get; init; }
}
