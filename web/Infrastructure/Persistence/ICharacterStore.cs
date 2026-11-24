using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Infrastructure.Persistence;

public interface ICharacterStore
{
    Task<IReadOnlyList<Character>> GetCharactersAsync(CancellationToken cancellationToken = default);
    Task<Character?> GetCharacterAsync(int id, CancellationToken cancellationToken = default);

    async Task<IReadOnlyDictionary<int, Character>> GetCharacterMapAsync(CancellationToken cancellationToken = default) =>
        (await GetCharactersAsync(cancellationToken)).ToDictionary(character => character.Id);

    Task<Character> GetCharacterForDayAsync(int dayNumber, CancellationToken cancellationToken = default);
    Task<Character> GetRandomCharacterAsync(DateOnly availableOn, CancellationToken cancellationToken = default);

    /// <summary>Every character, released or not; for resolving names of ids that are already recorded.</summary>
    Task<IReadOnlyList<CharacterLookup>> GetLookupsAsync(CancellationToken cancellationToken = default);

    /// <summary>The characters a player may guess in a game dated <paramref name="date"/>.</summary>
    Task<IReadOnlyList<CharacterLookup>> GetLookupsAvailableOnAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<CharacterLookup?> GetLookupAsync(int id, CancellationToken cancellationToken = default);
}
