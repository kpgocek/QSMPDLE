using Microsoft.EntityFrameworkCore;
using QSMPDLE.Web.DTOs;

namespace QSMPDLE.Web.Data;

public sealed class MemberRepository(QsmpdleDbContext Context) : IMemberRepository
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
