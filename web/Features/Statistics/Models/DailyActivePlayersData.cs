namespace QSMPDLE.Web.Features.Statistics.Models;

public sealed record DailyActivePlayersData
{
    public required DateOnly Date { get; init; }
    public required int PlayerCount { get; init; }
}
