namespace QSMPDLE.Web.Features.Statistics.Models;

public sealed record RetentionStats
{
    public required double D1Retention { get; init; }
    public required double D7Retention { get; init; }
    public required double D30Retention { get; init; }

    public required double D1PlusRetention { get; init; }
    public required double D7PlusRetention { get; init; }
    public required double D30PlusRetention { get; init; }
}
