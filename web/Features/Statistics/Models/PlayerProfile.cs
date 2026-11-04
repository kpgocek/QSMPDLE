namespace QSMPDLE.Web.Features.Statistics.Models;

public sealed record PlayerProfile
{
    public required PlayerStats Stats { get; init; }

    public int DailyGamesPlayed { get; init; }
    public int DailyGamesWon { get; init; }
    public double DailyWinRate { get; init; }
    public int[] GuessDistribution { get; init; } = [];

    public int PracticeGamesStarted { get; init; }
    public int PracticeGamesFinished { get; init; }
    public int PracticeGamesWon { get; init; }

    public DateOnly? MemberSince { get; init; }
    public GlobalStatsView? GlobalStats { get; init; }
    public PlayerCharacterStats? CharacterStats { get; init; }
}
