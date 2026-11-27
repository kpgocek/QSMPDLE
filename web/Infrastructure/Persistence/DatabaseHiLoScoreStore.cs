using Microsoft.EntityFrameworkCore;

namespace QSMPDLE.Web.Infrastructure.Persistence;

public sealed class DatabaseHiLoScoreStore(IDbContextFactory<ApplicationDbContext> DbContextFactory) : IHiLoScoreStore
{
    public async Task<int> GetBestScoreAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        await using var database = await DbContextFactory.CreateDbContextAsync(cancellationToken);

        return await database.HiLoScores
            .AsNoTracking()
            .Where(score => score.PlayerId == playerId)
            .Select(score => score.BestScore)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task RecordScoreAsync(Guid playerId, int score, CancellationToken cancellationToken = default)
    {
        if (playerId == Guid.Empty)
        {
            throw new InvalidOperationException("Cannot save a Hi-Lo score without a valid player id.");
        }

        await using var database = await DbContextFactory.CreateDbContextAsync(cancellationToken);

        // One statement, so two tabs of the same player cannot lower the best score.
        await database.Database.ExecuteSqlAsync($"""
                                                 INSERT INTO "HiLoScores" ("PlayerId", "BestScore") VALUES ({playerId}, {score})
                                                 ON CONFLICT ("PlayerId") DO UPDATE SET "BestScore" = EXCLUDED."BestScore"
                                                 WHERE "HiLoScores"."BestScore" < EXCLUDED."BestScore"
                                                 """, cancellationToken);
    }
}
