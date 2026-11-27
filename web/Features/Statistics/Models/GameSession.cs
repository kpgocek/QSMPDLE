using System.Linq.Expressions;
using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.Statistics.Models;

public sealed class GameSession
{
    /// <summary><see cref="Outcome" /> is not <see cref="GameOutcome.InProgress" />, for queries.</summary>
    public static readonly Expression<Func<GameSession, bool>> IsDecided = game =>
        game.FinishedOnUtc != null
        || game.IsWon
        || game.Guesses.Any(guess => guess.GuessedCharacterId == game.TargetCharacterId)
        || (game.TargetCharacterId != 0 && game.Guesses.Count >= GameRules.MaxGuesses);

    public int Id { get; set; }
    public required Guid GameId { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }

    public int? PuzzleId { get; set; }
    public SessionCategory SessionCategory { get; set; }
    public EntryPoint FirstEntryPoint { get; set; }
    public bool IsLegacyDuplicate { get; set; }

    public int TargetCharacterId { get; set; }

    public Ruleset Ruleset { get; set; }

    public DateTimeOffset StartedOnUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedOnUtc { get; set; }

    public bool IsWon { get; set; }

    public ICollection<GameGuess> Guesses { get; set; } = [];

    /// <summary>
    ///     Needs <see cref="Guesses" /> loaded. They decide too: finishes used to be saved separately and could be lost.
    /// </summary>
    public GameOutcome Outcome
    {
        get
        {
            var outcome = GameRules.GetOutcome(TargetCharacterId, GuessedCharacterIds);
            if (IsWon || outcome == GameOutcome.Won)
            {
                return GameOutcome.Won;
            }

            return FinishedOnUtc.HasValue ? GameOutcome.Lost : outcome;
        }
    }

    public IReadOnlyList<int> GuessedCharacterIds =>
    [
        .. Guesses.OrderBy(guess => guess.GuessOrder).ThenBy(guess => guess.Id)
            .Select(guess => guess.GuessedCharacterId)
    ];

    /// <summary>
    ///     Records the guesses this session is missing from the player's whole board. A board that contradicts
    ///     the recorded guesses, or a game that is already decided, changes nothing, so retries and stale tabs
    ///     cannot add or reorder guesses.
    /// </summary>
    public void RecordGuesses(IReadOnlyList<int> guessedCharacterIds, DateTimeOffset recordedOnUtc)
    {
        var recorded = GuessedCharacterIds;
        if (Outcome != GameOutcome.InProgress
            || guessedCharacterIds.Count > GameRules.MaxGuesses
            || recorded.Count >= guessedCharacterIds.Count
            || !recorded.SequenceEqual(guessedCharacterIds.Take(recorded.Count)))
        {
            return;
        }

        var nextOrder = Guesses.Count == 0 ? 0 : Guesses.Max(guess => guess.GuessOrder) + 1;
        for (var index = recorded.Count; index < guessedCharacterIds.Count; index++)
        {
            Guesses.Add(new GameGuess
            {
                GameId = GameId,
                GuessOrder = nextOrder++,
                GuessedCharacterId = guessedCharacterIds[index],
                // Earlier guesses are only missing because recording them failed, so their time is unknown.
                GuessedOnUtc = index == guessedCharacterIds.Count - 1 ? recordedOnUtc : null
            });
        }

        var outcome = GameRules.GetOutcome(TargetCharacterId, guessedCharacterIds);
        IsWon = outcome == GameOutcome.Won;
        if (outcome != GameOutcome.InProgress)
        {
            FinishedOnUtc = recordedOnUtc;
        }
    }
}
