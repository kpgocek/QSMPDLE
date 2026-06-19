using QSMPDLE.Web.Features.Characters.DTOs;

namespace QSMPDLE.Web.Features.Characters.Repositories;

public interface ICharacterRepository
{
    Task<List<CharacterLookup>> GetLookupAsync();
}
