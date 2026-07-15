namespace QSMPDLE.Web.Features.Statistics.Models;

public sealed record PlayerCharacterStats
{
    public int? MostGuessedCharacterId { get; init; }
    public string? MostGuessedCharacterName { get; init; }
    public long MostGuessedCount { get; init; }

    public int? MostCorrectlyGuessedCharacterId { get; init; }
    public string? MostCorrectlyGuessedCharacterName { get; init; }
    public long MostCorrectlyGuessedCount { get; init; }
}
