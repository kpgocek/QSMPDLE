using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.Statistics.Models;

public sealed class PlayerStats
{
    public const int CurrentVersion = 3;

    public int Version { get; set; } = CurrentVersion;

    public Guid Id { get; set; } = Guid.NewGuid();

    public int GamesPlayed { get; set; }

    public int GamesWon { get; set; }

    public int CurrentStreak { get; set; }

    public int MaxStreak { get; set; }

    /// <summary>Wins by number of guesses.</summary>
    public int[] GuessDistribution { get; set; } = new int[GameRules.MaxGuesses];

    public double WinRate =>
        GamesPlayed == 0
            ? 0
            : (double)GamesWon / GamesPlayed * 100;
}
