using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Infrastructure.Persistence;

public class CachedDatabaseCharacterStore(ApplicationDbContext Gameplay, IMemoryCache Cache) : ICharacterStore
{
    public async Task<IReadOnlyList<Character>> GetCharactersAsync(CancellationToken cancellationToken = default)
    {
        var characters = await Cache.GetOrCreateAsync("qsmpdle-characters", async entry =>
        {
            entry.Priority = CacheItemPriority.NeverRemove;

            return await Gameplay.Characters.AsNoTracking().ToListAsync(cancellationToken);
        }) ?? throw new InvalidOperationException();

        return characters.AsReadOnly();
    }

    public async Task<Character?> GetCharacterAsync(int id, CancellationToken cancellationToken = default)
    {
        var characters = await GetCharactersAsync(cancellationToken);

        return characters.FirstOrDefault(ch => ch.Id.Equals(id));
    }

    public async Task<IReadOnlyList<CharacterLookup>> GetLookupsAsync(CancellationToken cancellationToken = default)
    {
        var lookups = await Cache.GetOrCreateAsync<List<CharacterLookup>>("qsmpdle-character-lookups", async entry =>
        {
            entry.Priority = CacheItemPriority.NeverRemove;

            var characters = await GetCharactersAsync(cancellationToken);

            return characters.Select(ch => new CharacterLookup(ch.Id, ch.Name, ch.MinecraftUsername, ch.Aliases, ch.IconUrl)).OrderBy(l => l.Name).ToList();
        });

        return lookups ?? [];
    }

    public async Task<CharacterLookup?> GetLookupAsync(int id, CancellationToken cancellationToken = default)
    {
        var lookups = await GetLookupsAsync(cancellationToken);

        return lookups.FirstOrDefault(l => l.Id.Equals(id));
    }

    public async Task<Character> GetCharacterForDayAsync(int dayNumber, CancellationToken cancellationToken = default)
    {
        return await Cache.GetOrCreateAsync($"daily-character-{dayNumber}", async (entry) =>
        {
            entry.AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromDays(1);

            var characterId = await Gameplay.DailyGames
                    .AsNoTracking()
                    .Where(x => x.Id == dayNumber)
                    .Select(x => (int?)x.Character.Id)
                    .SingleOrDefaultAsync(cancellationToken);

            if (characterId is not null)
            {
                var character = await GetCharacterAsync(characterId.Value, cancellationToken);
                if (character is not null)
                {
                    return character;
                }
            }

            var characters = await GetCharactersAsync(cancellationToken);
            if (characters.Count == 0)
            {
                throw new InvalidOperationException("Cannot start a game without characters.");
            }

            var random = new Random(dayNumber);
            var index = random.Next(characters.Count);

            return characters[index];

        }) ?? throw new InvalidOperationException();
    }

    public async Task<Character> GetRandomCharacterAsync(CancellationToken cancellationToken = default)
    {
        var characters = await GetCharactersAsync(cancellationToken);

        var random = new Random();
        var index = random.Next(characters.Count);

        return characters[index];
    }
}
