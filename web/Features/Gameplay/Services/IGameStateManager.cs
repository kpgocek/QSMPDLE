namespace QSMPDLE.Web.Features.Gameplay.Services;

using Models;
using QSMPDLE.Web.Extensions;

public interface IGameStateManager
{
    GameState GameState { get; }

    /// <summary>True while this game is the player's first ever, so the board can explain its clues.</summary>
    bool IsFirstGame { get; }

    /// <summary>The date that decides which characters and clues this game may use.</summary>
    DateOnly EffectiveDate { get; }

    IReadOnlySet<ClueGroup> ActiveClues { get; }

    Task<LoadGameResult> StartGameAsync(EntryPoint entryPoint, int? puzzleId = null, CancellationToken cancellationToken = default);
    Task StartNewPracticeGameAsync(CancellationToken cancellationToken = default);
    Task<GuessResult?> MakeGuessAsync(int characterId, CancellationToken cancellationToken = default);
    Task<string> GetTargetName(CancellationToken cancellationToken = default);

    Task MarkPopupAsSeenAsync(CancellationToken cancellationToken = default);
}
