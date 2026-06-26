using Microsoft.EntityFrameworkCore;
using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Infrastructure.Persistence;

public sealed class DatabaseGameStatsStore(TelemetryDbContext telemetry) : IGameStatsStore
{
    public async Task<GameSession> LoadOrNewAsync(Guid gameId)
    {
        var stats = await telemetry.GameStats.FirstOrDefaultAsync(stats => stats.GameId == gameId);

        return stats ?? new GameSession { GameId = gameId };
    }
    public async Task SaveAsync(GameSession stats)
    {
        telemetry.GameStats.Update(stats);

        await telemetry.SaveChangesAsync();
    }
}