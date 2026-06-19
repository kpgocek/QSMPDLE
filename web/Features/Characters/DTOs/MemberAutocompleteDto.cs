namespace QSMPDLE.Web.DTOs;

public sealed record MemberLookup(int Id, string Name, string? MinecraftUsername, List<string> Aliases, string CharacterIconUrl);
