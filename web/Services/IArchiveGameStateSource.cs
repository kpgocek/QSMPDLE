using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Services;

public interface IArchiveGameStateSource
{
    Task<GameState?> LoadAsync(GameMode mode, int dayNumber, CancellationToken cancellationToken = default);
}
