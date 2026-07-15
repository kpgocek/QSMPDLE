namespace QSMPDLE.Web.Features.Statistics.Models;

public sealed record RetentionStats
{
    public required double D1Retention { get; init; }
    public required double D7Retention { get; init; }
    public required double D14Retention { get; init; }
    public required double D30Retention { get; init; }
}
