using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.Statistics.Models;

public sealed record ModePopularityEntry
{
    public required GameMode Mode { get; init; }
    public required long SessionCount { get; init; }
    public required double Share { get; init; }
}