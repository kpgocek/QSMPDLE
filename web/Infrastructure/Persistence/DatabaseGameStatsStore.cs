using Microsoft.EntityFrameworkCore;
using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Infrastructure.Persistence;

public sealed class DatabaseGameStatsStore(QsmpdleDbContext Qsmp) : IGameStatsStore
{
    public async Task<GameSession> LoadOrNewAsync(Guid gameId)
    {
        var stats = await Qsmp.GameStats.FirstOrDefaultAsync(stats => stats.GameId == gameId);

        return stats ?? new GameSession { GameId = gameId };
    }
    public async Task SaveAsync(GameSession stats)
    {
        Qsmp.GameStats.Update(stats);

        await Qsmp.SaveChangesAsync();
    }
}