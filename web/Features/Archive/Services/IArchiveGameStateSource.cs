using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.Archive.Services;

public interface IArchiveGameStateSource
{
    Task<GameState?> LoadAsync(GameMode mode, int dayNumber, CancellationToken cancellationToken = default);
}
