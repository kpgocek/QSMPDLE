using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QSMPDLE.Web.Data;
using QSMPDLE.Web.Features.Characters.DTOs;

namespace QSMPDLE.Web.Features.Characters.Repositories;

public sealed class CharacterRepository(QsmpdleDbContext Context, IMemoryCache Cache) : ICharacterRepository
{
    public async Task<List<CharacterLookup>> GetLookupAsync()
    {
        var lookups = await Cache.GetOrCreateAsync<List<CharacterLookup>>("character-lookups", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1);

            return await Context.Characters
                .OrderBy(m => m.Name)
                .Select(m => new CharacterLookup(m.Id, m.Name, m.MinecraftUsername, m.Aliases, m.IconUrl))
                .ToListAsync();
        });

        return lookups ?? [];
    }
}
