using Microsoft.EntityFrameworkCore;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Infrastructure.Persistence;

namespace QSMPDLE.Web.Features.Gameplay.Services;

/// <summary>
///     Keeps the DailyGames table filled ahead of today, replacing the retired offline generator.
///     Rows already written are never changed, so a scheduled answer stays fixed once it exists.
/// </summary>
public sealed class DailyScheduleService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IDayService dayService,
    TimeProvider timeProvider) : IDailyScheduleService
{
    public const int DaysAhead = 60;

    public async Task<int> EnsureScheduledAsync(CancellationToken cancellationToken = default)
    {
        var today = dayService.GetArchiveDayNumber(DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));
        var horizon = today + DaysAhead;

        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Day numbers are the row ids the daily lookup reads, so history is ordered by them.
        var history = await database.DailyGames
            .AsNoTracking()
            .OrderBy(game => game.Id)
            .Select(game => new { game.Id, game.CharacterId })
            .ToListAsync(cancellationToken);

        var lastScheduled = history.Count > 0 ? history[^1].Id : 0;

        // Days up to today without a row were already served by the fallback pick; scheduling them
        // now would change an answer players may have seen.
        var firstDay = Math.Max(lastScheduled + 1, today + 1);
        if (firstDay > horizon)
        {
            return 0;
        }

        var availableFrom = await database.Characters
            .AsNoTracking()
            .Select(character => new { character.Id, character.AvailableFrom })
            .ToDictionaryAsync(character => character.Id, character => character.AvailableFrom, cancellationToken);

        var picks = DailySchedulePlanner.Plan(
            availableFrom.Keys,
            history.ConvertAll(game => game.CharacterId),
            horizon - firstDay + 1,
            firstDay,
            (id, offset) => availableFrom[id] is not { } from || from <= dayService.GetArchiveDate(firstDay + offset));

        database.DailyGames.AddRange(picks.Select((characterId, offset) => new DailyGame
        {
            Id = firstDay + offset, Date = dayService.GetArchiveDate(firstDay + offset), CharacterId = characterId
        }));

        await database.SaveChangesAsync(cancellationToken);
        return picks.Count;
    }
}
