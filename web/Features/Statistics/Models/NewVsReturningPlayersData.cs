namespace QSMPDLE.Web.Features.Statistics.Models;

public sealed record NewVsReturningPlayersData
{
    public required DateOnly Date { get; init; }
    public required long NewPlayers { get; init; }
    public required long ReturningPlayers { get; init; }
}
