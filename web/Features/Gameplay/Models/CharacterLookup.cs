namespace QSMPDLE.Web.Features.Gameplay.Models;

public sealed record CharacterLookup(int Id, string Name, string? MinecraftUsername, List<string> Aliases, string CharacterIconUrl);
