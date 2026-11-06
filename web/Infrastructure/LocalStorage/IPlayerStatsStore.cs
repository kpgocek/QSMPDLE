using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Infrastructure.LocalStorage;

public interface IPlayerStatsStore
{
    Task<PlayerStats> LoadAsync();

    /// <summary>Resolves only the anonymous identity, without deriving statistics from game history.</summary>
    async Task<Guid> GetPlayerIdAsync() => (await LoadAsync()).Id;

    Task SaveAsync(PlayerStats stats);
    Task ClearAsync();
}
