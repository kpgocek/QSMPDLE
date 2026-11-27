using System.Collections.Frozen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QSMPDLE.Web.Features.Gameplay.Helpers;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;

namespace QSMPDLE.Web.Infrastructure.Persistence;

/// <summary>
/// One catalog of every character, released or not, is shared by every circuit and reloaded after
/// each UTC midnight so database edits appear without a restart. Release dates are applied when the
/// catalog is read, never baked into it, so a cached catalog cannot outlive a release. Loads are
/// gated so a cold start or the midnight day rollover runs a single query instead of one per
/// concurrent player, and they ignore the caller's token so one dropped circuit cannot fail the
/// load that other players are waiting on.
/// </summary>
public class CachedDatabaseCharacterStore(
    IDbContextFactory<ApplicationDbContext> DbContextFactory,
    IMemoryCache Cache,
    IDayService DayService,
    TimeProvider TimeProvider,
    ILogger<CachedDatabaseCharacterStore> Logger) : ICharacterStore
{
    private const string CatalogKey = "qsmpdle-character-catalog";

    private static readonly SemaphoreSlim LoadGate = new(1, 1);

    public async Task<IReadOnlyList<Character>> GetCharactersAsync(CancellationToken cancellationToken = default) =>
        (await GetCatalogAsync(cancellationToken)).Characters;

    public async Task<IReadOnlyDictionary<int, Character>> GetCharacterMapAsync(CancellationToken cancellationToken = default) =>
        (await GetCatalogAsync(cancellationToken)).CharactersById;

    public async Task<Character?> GetCharacterAsync(int id, CancellationToken cancellationToken = default) =>
        (await GetCatalogAsync(cancellationToken)).CharactersById.GetValueOrDefault(id);

    public async Task<IReadOnlyList<CharacterLookup>> GetLookupsAsync(CancellationToken cancellationToken = default) =>
        (await GetCatalogAsync(cancellationToken)).Lookups;

    public async Task<IReadOnlyList<CharacterLookup>> GetLookupsAvailableOnAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var catalog = await GetCatalogAsync(cancellationToken);
        return catalog.Lookups.Where(lookup => catalog.CharactersById[lookup.Id].IsAvailableOn(date)).ToList();
    }

    public async Task<CharacterLookup?> GetLookupAsync(int id, CancellationToken cancellationToken = default) =>
        (await GetCatalogAsync(cancellationToken)).LookupsById.GetValueOrDefault(id);

    public async Task<Character> GetCharacterForDayAsync(int dayNumber, CancellationToken cancellationToken = default)
    {
        var key = $"daily-character-{dayNumber}";
        if (Cache.TryGetValue(key, out Character? cached) && cached is not null)
            return cached;

        var catalog = await GetCatalogAsync(cancellationToken);

        await LoadGate.WaitAsync(cancellationToken);
        try
        {
            if (Cache.TryGetValue(key, out cached) && cached is not null)
                return cached;

            var character = await LoadCharacterForDayAsync(dayNumber, catalog);
            Cache.Set(key, character, TimeSpan.FromDays(1));
            return character;
        }
        finally
        {
            LoadGate.Release();
        }
    }

    public async Task<Character> GetRandomCharacterAsync(DateOnly availableOn, CancellationToken cancellationToken = default)
    {
        var characters = (await GetCharactersAsync(cancellationToken)).Where(character => character.IsAvailableOn(availableOn)).ToList();

        return characters[Random.Shared.Next(characters.Count)];
    }

    private async Task<CharacterCatalog> GetCatalogAsync(CancellationToken cancellationToken)
    {
        if (Cache.TryGetValue(CatalogKey, out CharacterCatalog? catalog) && catalog is not null)
            return catalog;

        await LoadGate.WaitAsync(cancellationToken);
        try
        {
            if (Cache.TryGetValue(CatalogKey, out catalog) && catalog is not null)
                return catalog;

            await using var gameplay = await DbContextFactory.CreateDbContextAsync(CancellationToken.None);
            var characters = await gameplay.Characters.AsNoTracking().ToListAsync(CancellationToken.None);

            catalog = CharacterCatalog.Create(characters);
            var nextMidnight = new DateTimeOffset(TimeProvider.GetUtcNow().UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
            Cache.Set(CatalogKey, catalog, new MemoryCacheEntryOptions
            {
                Priority = CacheItemPriority.NeverRemove,
                AbsoluteExpiration = nextMidnight,
            });
            return catalog;
        }
        finally
        {
            LoadGate.Release();
        }
    }

    private async Task<Character> LoadCharacterForDayAsync(int dayNumber, CharacterCatalog catalog)
    {
        var date = DayService.GetArchiveDate(dayNumber);
        await using var gameplay = await DbContextFactory.CreateDbContextAsync(CancellationToken.None);

        var scheduled = await gameplay.DailyGames
            .Where(x => x.Id == dayNumber)
            .SingleOrDefaultAsync(CancellationToken.None);

        if (scheduled is not null && catalog.CharactersById.TryGetValue(scheduled.CharacterId, out var character))
        {
            // A played day keeps its answer, even if the character's release date was moved past it later.
            return character.IsAvailableOn(date) || dayNumber < DayService.GetTodayDayNumber()
                ? character
                : await ReplaceUnreleasedScheduleAsync(gameplay, scheduled, date, catalog);
        }

        var released = catalog.Characters.Where(candidate => candidate.IsAvailableOn(date)).ToList();
        if (released.Count == 0)
            throw new InvalidOperationException("Cannot start a game without characters.");

        var random = new Random(dayNumber);
        return released[random.Next(released.Count)];
    }

    /// <summary>
    /// A row scheduled before its character's release date was set would leak that character, so the
    /// day is re-planned from the characters released by then and saved, keeping every later load in agreement.
    /// </summary>
    private async Task<Character> ReplaceUnreleasedScheduleAsync(ApplicationDbContext gameplay, DailyGame scheduled, DateOnly date, CharacterCatalog catalog)
    {
        var history = await gameplay.DailyGames
            .AsNoTracking()
            .Where(game => game.Id < scheduled.Id)
            .OrderBy(game => game.Id)
            .Select(game => game.CharacterId)
            .ToListAsync(CancellationToken.None);

        var released = catalog.Characters.Where(candidate => candidate.IsAvailableOn(date)).Select(candidate => candidate.Id).ToList();
        if (released.Count == 0)
            throw new InvalidOperationException("Cannot start a game without characters.");

        var replacement = DailySchedulePlanner.Plan(released, history, 1, seed: scheduled.Id)[0];
        scheduled.CharacterId = replacement;
        await gameplay.SaveChangesAsync(CancellationToken.None);

        Logger.LogError("Daily game {DayNumber} was scheduled with a character released after its date; it was re-planned and saved.", scheduled.Id);
        return catalog.CharactersById[replacement];
    }

    private sealed record CharacterCatalog(
        IReadOnlyList<Character> Characters,
        FrozenDictionary<int, Character> CharactersById,
        IReadOnlyList<CharacterLookup> Lookups,
        FrozenDictionary<int, CharacterLookup> LookupsById)
    {
        public static CharacterCatalog Create(List<Character> characters)
        {
            var lookups = characters
                .Select(ch => new CharacterLookup(ch.Id, ch.Name, ch.MinecraftUsername, ch.Aliases, CharacterUrlHelper.GetPortraitFolder(ch.PortraitId)))
                .OrderBy(l => l.Name)
                .ToList();

            return new CharacterCatalog(
                characters.AsReadOnly(),
                characters.ToFrozenDictionary(ch => ch.Id),
                lookups.AsReadOnly(),
                lookups.ToFrozenDictionary(l => l.Id));
        }
    }
}
