using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.Gameplay.Services;

/// <summary>
/// The single place that decides which released content a game may use. A puzzle is judged by
/// its own date, so archive days keep the content they had even after later releases.
/// </summary>
public interface IGameplayAvailabilityService
{
    /// <summary>The puzzle's date, or today (UTC) for a practice game.</summary>
    DateOnly GetEffectiveDate(int? puzzleId);

    IReadOnlySet<ClueGroup> GetActiveClues(DateOnly date);

    /// <summary>Follows the clues active on the date, so it moves with the configured premiere, not with a deployment.</summary>
    Ruleset GetRuleset(DateOnly date);
}
