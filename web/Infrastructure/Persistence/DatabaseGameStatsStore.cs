using Microsoft.EntityFrameworkCore;
using Npgsql;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Infrastructure.Persistence;

public sealed class DatabaseGameStatsStore(IDbContextFactory<ApplicationDbContext> dbContextFactory) : IGameStatsStore
{
    public async Task<IEnumerable<GameSession>> GetPlayerGames(Guid playerId, SessionCategory? category = null)
    {
        await using var database = await dbContextFactory.CreateDbContextAsync();

        return await database.GameStats
            .AsNoTracking()
            .Include(game => game.Guesses)
            .Where(game => game.PlayerId.Equals(playerId))
            .Where(game => !game.IsLegacyDuplicate)
            .Where(game => category == null || game.SessionCategory == category)
            .ToListAsync();
    }

    public async Task<List<GameSession>> GetPlayerCanonicalGamesByPuzzleRangeAsync(Guid playerId, int startPuzzleId,
        int endPuzzleId)
    {
        await using var database = await dbContextFactory.CreateDbContextAsync();

        return await database.GameStats
            .AsNoTracking()
            .Include(session => session.Guesses)
            .Where(session => session.PlayerId == playerId
                              && session.SessionCategory == SessionCategory.CanonicalPuzzle
                              && !session.IsLegacyDuplicate
                              && session.PuzzleId.HasValue
                              && session.PuzzleId >= startPuzzleId
                              && session.PuzzleId <= endPuzzleId)
            .ToListAsync();
    }

    public async Task<GameSession?> GetActiveCanonicalSessionAsync(Guid playerId, int puzzleId)
    {
        await using var database = await dbContextFactory.CreateDbContextAsync();

        return await CanonicalSessions(database, playerId, puzzleId).SingleOrDefaultAsync();
    }

    public async Task<GameSession> RecordGuessAsync(GameSession game, IReadOnlyList<int> guessedCharacterIds,
        DateTimeOffset recordedOnUtc)
    {
        if (game.PlayerId == Guid.Empty)
        {
            throw new InvalidOperationException("Cannot save game stats without a valid player id.");
        }

        await using var database = await dbContextFactory.CreateDbContextAsync();
        await using var transaction = await database.Database.BeginTransactionAsync();

        var session = await LockAsync(database, game);
        if (session is null)
        {
            game.RecordGuesses(guessedCharacterIds, recordedOnUtc);
            database.GameStats.Add(game);
            try
            {
                await database.SaveChangesAsync();
                await transaction.CommitAsync();
                return game;
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
                                                      {
                                                          SqlState: PostgresErrorCodes.UniqueViolation
                                                      })
            {
                // Another tab created the game first. EF rolled back to its savepoint, so the transaction is still usable.
                database.ChangeTracker.Clear();
                session = await LockAsync(database, game);
                if (session is null)
                {
                    throw;
                }
            }
        }

        // Read after the lock is held: a join in the locking query would see the guesses from before the wait.
        await database.Entry(session).Collection(stored => stored.Guesses).LoadAsync();
        if (session.GameId != game.GameId)
        {
            return session;
        }

        session.RecordGuesses(guessedCharacterIds, recordedOnUtc);
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
        return session;
    }

    private static async Task<GameSession?> LockAsync(ApplicationDbContext database, GameSession game)
    {
        var rows = game.SessionCategory == SessionCategory.CanonicalPuzzle
            ? database.GameStats.FromSql($"""
                                          SELECT * FROM "GameStats"
                                          WHERE "PlayerId" = {game.PlayerId} AND "PuzzleId" = {game.PuzzleId} AND "SessionCategory" = 0 AND NOT "IsLegacyDuplicate"
                                          FOR UPDATE
                                          """)
            : database.GameStats.FromSql($"""SELECT * FROM "GameStats" WHERE "GameId" = {game.GameId} FOR UPDATE""");

        // Materialized before composing, so FOR UPDATE stays the outermost clause.
        return (await rows.ToListAsync()).SingleOrDefault();
    }

    private static IQueryable<GameSession>
        CanonicalSessions(ApplicationDbContext database, Guid playerId, int puzzleId) =>
        database.GameStats
            .AsNoTracking()
            .Include(session => session.Guesses)
            .Where(session => session.PlayerId == playerId
                              && session.SessionCategory == SessionCategory.CanonicalPuzzle
                              && !session.IsLegacyDuplicate
                              && session.PuzzleId == puzzleId);
}
