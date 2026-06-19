using QSMPDLE.Web.DTOs;

namespace QSMPDLE.Web.Features.Characters.Repositories;

public interface ICharacterRepository
{
    Task<List<MemberLookup>> GetLookupAsync();
}
