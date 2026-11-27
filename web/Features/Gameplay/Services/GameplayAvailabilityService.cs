using Microsoft.Extensions.Options;
using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.Gameplay.Services;

public sealed class GameplayAvailabilityService(IDayService dayService, IOptions<GameplayOptions> options) : IGameplayAvailabilityService
{
    public DateOnly GetEffectiveDate(int? puzzleId) =>
        dayService.GetArchiveDate(puzzleId ?? dayService.GetTodayDayNumber());

    public IReadOnlySet<ClueGroup> GetActiveClues(DateOnly date) =>
        Enum.GetValues<ClueGroup>()
            .Where(clue => !options.Value.CluePremieres.TryGetValue(clue, out var premiere) || premiere <= date)
            .ToHashSet();

    public Ruleset GetRuleset(DateOnly date) => GetActiveClues(date).Count > 0 ? Ruleset.V2 : Ruleset.Classic;
}
