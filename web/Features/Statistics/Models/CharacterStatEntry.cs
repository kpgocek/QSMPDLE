namespace QSMPDLE.Web.Features.Statistics.Models;

public sealed record CharacterStatEntry
{
    public required int CharacterId { get; init; }
    public required string CharacterName { get; init; }
    public required long Count { get; init; }
}

public sealed record CharacterWindowStats
{
    public required IReadOnlyList<CharacterStatEntry> Yesterday { get; init; }
    public required IReadOnlyList<CharacterStatEntry> PastWeek { get; init; }
    public required IReadOnlyList<CharacterStatEntry> PastMonth { get; init; }
}

public sealed record GlobalCharacterStats
{
    public required CharacterWindowStats MostConfusing { get; init; }
    public required CharacterWindowStats Easiest { get; init; }
    public required CharacterWindowStats TheIndicator { get; init; }
    public required CharacterWindowStats TheOpener { get; init; }
}
