using QSMPDLE.Web.Data.Gameplay;

namespace QSMPDLE.Web.Services;

public interface IPlayerStatsStore
{
    Task<PlayerStats> LoadAsync();

    Task SaveAsync(PlayerStats stats);
    Task ClearAsync();
}
