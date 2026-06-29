using Microsoft.EntityFrameworkCore;
using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Infrastructure.Persistence;

public sealed class DatabaseGameStatsStore(ApplicationDbContext database) : IGameStatsStore
{
    public async Task<IEnumerable<GameSession>> GetPlayerGames(Guid playerId)
    {
        return await database.GameStats.Where(game => game.PlayerId.Equals(playerId)).ToListAsync();
    }

    public async Task<GameSession> LoadOrNewAsync(Guid gameId)
    {
        var stats = await database.GameStats
            .Include(stats => stats.Guesses)
            .FirstOrDefaultAsync(stats => stats.GameId == gameId);

        return stats ?? new GameSession { GameId = gameId };
    }
    public async Task SaveAsync(GameSession stats)
    {
        if (stats.PlayerId == Guid.Empty)
        {
            throw new InvalidOperationException("Cannot save game stats without a valid player id.");
        }

        database.GameStats.Update(stats);

        await database.SaveChangesAsync();
    }

    public async Task<GlobalStatsView> GetGlobalStatsAsync()
    {
        return await database.Set<GlobalStatsView>()
            .AsNoTracking()
            .SingleAsync();
    }




}
