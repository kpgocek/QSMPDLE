using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.Gameplay.Stores;

public interface IPlayerStatsStore
{
    Task<PlayerStats> LoadAsync();

    Task SaveAsync(PlayerStats stats);
    Task ClearAsync();
}
