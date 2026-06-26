using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Infrastructure.LocalStorage;

public interface IPlayerStatsStore
{
    Task<PlayerStats> LoadAsync();

    Task SaveAsync(PlayerStats stats);
    Task ClearAsync();
}
