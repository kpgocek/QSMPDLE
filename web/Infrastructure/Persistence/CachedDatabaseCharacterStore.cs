using System.Collections.Frozen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Infrastructure.Persistence;

/// <summary>
/// Character data is immutable at runtime, so one catalog is shared by every circuit. Loads are
/// gated so a cold start or the midnight day rollover runs a single query instead of one per
/// concurrent player, and they ignore the caller's token so one dropped circuit cannot fail the
/// load that other players are waiting on.
/// </summary>
public class CachedDatabaseCharacterStore(
    IDbContextFactory<ApplicationDbContext> DbContextFactory,
    IMemoryCache Cache) : ICharacterStore
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

    public async Task<Character> GetRandomCharacterAsync(CancellationToken cancellationToken = default)
    {
        var characters = await GetCharactersAsync(cancellationToken);

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
            Cache.Set(CatalogKey, catalog, new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
            return catalog;
        }
        finally
        {
            LoadGate.Release();
        }
    }

    private async Task<Character> LoadCharacterForDayAsync(int dayNumber, CharacterCatalog catalog)
    {
        await using var gameplay = await DbContextFactory.CreateDbContextAsync(CancellationToken.None);

        var characterId = await gameplay.DailyGames
            .AsNoTracking()
            .Where(x => x.Id == dayNumber)
            .Select(x => (int?)x.Character.Id)
            .SingleOrDefaultAsync(CancellationToken.None);

        if (characterId is not null && catalog.CharactersById.TryGetValue(characterId.Value, out var scheduled))
            return scheduled;

        if (catalog.Characters.Count == 0)
            throw new InvalidOperationException("Cannot start a game without characters.");

        var random = new Random(dayNumber);
        return catalog.Characters[random.Next(catalog.Characters.Count)];
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
                .Select(ch => new CharacterLookup(ch.Id, ch.Name, ch.MinecraftUsername, ch.Aliases, ch.IconUrl))
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
