namespace QSMPDLE.Web.Features.Characters.DTOs;

public sealed record CharacterLookup(int Id, string Name, string? MinecraftUsername, List<string> Aliases, string CharacterIconUrl);
