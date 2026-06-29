namespace QSMPDLE.Web.Features.Gameplay.Services;

using Models;
using QSMPDLE.Web.Extensions;

public interface IGameStateManager
{
    GameState GameState { get; }
    Task<LoadGameResult> LoadOrCreateAsync(GameMode mode, int? dayNumber = null, CancellationToken cancellationToken = default);
    Task StartNewPracticeGameAsync(CancellationToken cancellationToken = default);
    Task<GuessResult?> MakeGuessAsync(int characterId, CancellationToken cancellationToken = default);
    Task<string> GetTargetName(CancellationToken cancellationToken = default);

    Task MarkPopupAsSeenAsync(CancellationToken cancellationToken = default);
    Task MarkStatsRecordedAsync(CancellationToken cancellationToken = default);
}
