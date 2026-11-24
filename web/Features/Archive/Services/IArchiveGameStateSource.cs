using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.Archive.Services;

public interface IArchiveGameStateSource
{
    Task<GameState?> LoadAsync(int dayNumber, CancellationToken cancellationToken = default);

    /// <summary>Day numbers with saved progress in this browser, or null when unknown, in which case every day is read.</summary>
    Task<IReadOnlySet<int>?> GetStoredDayNumbersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<int>?>(null);
}
