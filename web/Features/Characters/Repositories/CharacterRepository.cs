using Microsoft.EntityFrameworkCore;
using QSMPDLE.Web.Data;
using QSMPDLE.Web.DTOs;

namespace QSMPDLE.Web.Features.Characters.Repositories;

public sealed class CharacterRepository(QsmpdleDbContext Context) : ICharacterRepository
{
    public async Task<List<MemberLookup>> GetLookupAsync()
    {
        return await
            Context.Members
            .OrderBy(m => m.Name)
            .Select(m => new MemberLookup(m.Id, m.Name, m.MinecraftUsername, m.Aliases, m.CharacterIconUrl))
            .ToListAsync();
    }
}
