namespace QSMPDLE.Web.Features.Statistics.Models;

public sealed record GamesPerPlayerStats
{
    public required double Average { get; init; }
    public required double Median { get; init; }
    public required long Maximum { get; init; }
}
