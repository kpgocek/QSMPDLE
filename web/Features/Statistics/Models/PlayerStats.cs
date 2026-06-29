namespace QSMPDLE.Web.Features.Statistics.Models;

public sealed class PlayerStats
{
    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;

    public Guid Id { get; set; } = Guid.NewGuid();

    public int GamesPlayed { get; set; }

    public int GamesWon { get; set; }

    public int CurrentStreak { get; set; }

    public int MaxStreak { get; set; }

    public int? LastCompletedDayNumber { get; set; }
    public Guid LastPlayedDailyGameId { get; set; }

    public int[] GuessDistribution { get; set; } = new int[6];

    public double WinRate =>
        GamesPlayed == 0
            ? 0
            : (double)GamesWon / GamesPlayed * 100;
}
