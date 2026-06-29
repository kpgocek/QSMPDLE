using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.Statistics.Models;

public sealed class GameSession
{
    public int Id { get; set; }
    public required Guid GameId { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }

    public GameMode Mode { get; set; }
    public int? DailyNumber { get; set; }

    public int TargetCharacterId { get; set; }

    public DateTimeOffset StartedOnUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedOnUtc { get; set; }

    public TimeSpan? Duration => FinishedOnUtc.HasValue ? FinishedOnUtc.Value - StartedOnUtc : null;

    public bool IsWon { get; set; }

    public ICollection<GameGuess> Guesses { get; set; } = [];

    internal void AddGuess(int guessedCharacterId)
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
