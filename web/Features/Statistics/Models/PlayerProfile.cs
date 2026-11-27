namespace QSMPDLE.Web.Features.Statistics.Models;

public sealed record PlayerProfile
{
    /// <summary>Daily and archive games, the same numbers the result dialog shows.</summary>
    public required PlayerStats Stats { get; init; }

    public int PracticeGamesStarted { get; init; }
    public int PracticeGamesFinished { get; init; }
    public int PracticeGamesWon { get; init; }

    public int HiLoBestScore { get; init; }

    public PlayerCharacterStats? CharacterStats { get; init; }
}
