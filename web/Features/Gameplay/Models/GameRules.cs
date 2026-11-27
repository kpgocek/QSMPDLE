namespace QSMPDLE.Web.Features.Gameplay.Models;

public static class GameRules
{
    public const int MaxGuesses = 6;

    /// <summary>
    ///     The result a board of guesses alone proves. Without a known answer (0, in old sessions) it cannot prove a
    ///     loss.
    /// </summary>
    public static GameOutcome GetOutcome(int targetCharacterId, IReadOnlyCollection<int> guessedCharacterIds)
    {
        if (guessedCharacterIds.Contains(targetCharacterId))
        {
            return GameOutcome.Won;
        }

        return guessedCharacterIds.Count >= MaxGuesses && targetCharacterId != 0
            ? GameOutcome.Lost
            : GameOutcome.InProgress;
    }
}
