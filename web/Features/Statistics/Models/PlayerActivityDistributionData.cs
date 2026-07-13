namespace QSMPDLE.Web.Features.Statistics.Models;

public sealed record PlayerActivityDistributionData
{
    public required string Bucket { get; init; }
    public required int PlayerCount { get; init; }
}
