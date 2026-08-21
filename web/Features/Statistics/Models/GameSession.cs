using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.Statistics.Models;

public sealed class GameSession
{
    public int Id { get; set; }
    public required Guid GameId { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }

    public int? PuzzleId { get; set; }
    public SessionCategory SessionCategory { get; set; }
    public EntryPoint FirstEntryPoint { get; set; }
    public bool IsLegacyDuplicate { get; set; }

    // Compatibility data retained while existing telemetry is migrated.
    public GameMode Mode { get; set; }
    public int? DailyNumber { get; set; }

    public int TargetCharacterId { get; set; }

    public DateTimeOffset StartedOnUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedOnUtc { get; set; }

    public TimeSpan? Duration => FinishedOnUtc.HasValue ? FinishedOnUtc.Value - StartedOnUtc : null;

    public bool IsWon { get; set; }

    public ICollection<GameGuess> Guesses { get; set; } = [];

    public void AddGuess(int guessedCharacterId)
    {
        if (Guesses.Any(guess => guess.GuessedCharacterId == guessedCharacterId))
            return;

        Guesses.Add(new GameGuess
        {
            GameId = GameId,
            GuessOrder = Guesses.Count,
            GuessedCharacterId = guessedCharacterId
        });
    }
}
