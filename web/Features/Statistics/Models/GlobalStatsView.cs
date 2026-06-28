namespace QSMPDLE.Web.Features.Statistics.Models;

public sealed class GlobalStatsView
{
    public long TotalGames { get; init; }

    public long TotalPlayers { get; init; }

    public long TotalWins { get; init; }

    public double WinRate =>
        TotalGames == 0
            ? 0
            : (double)TotalWins / TotalGames * 100;
}