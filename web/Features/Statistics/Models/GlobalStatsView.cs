namespace QSMPDLE.Web.Features.Statistics.Models;

public sealed class GlobalStatsView
{
    public long TotalGames { get; init; }

    public long TotalPlayers { get; init; }

    public long TotalWins { get; init; }

    public double AverageGuessesToWin { get; init; }

    public long[] DailyGuessDistribution { get; init; } = new long[6];

    public long[] PracticeGuessDistribution { get; init; } = new long[6];

    public double WinRate =>
        TotalGames == 0
            ? 0
            : (double)TotalWins / TotalGames * 100;
}