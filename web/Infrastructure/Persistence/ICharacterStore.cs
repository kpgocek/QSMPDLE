using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Infrastructure.Persistence;

public interface ICharacterStore
{
    Task<IReadOnlyList<Character>> GetCharactersAsync(CancellationToken cancellationToken = default);
    Task<Character?> GetCharacterAsync(int id, CancellationToken cancellationToken = default);
    Task<Character> GetCharacterForDayAsync(int dayNumber, CancellationToken cancellationToken = default);
    Task<Character> GetRandomCharacterAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CharacterLookup>> GetLookupsAsync(CancellationToken cancellationToken = default);
}
