using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.Gameplay.Helpers;

/// <summary>
///     Owns every character URL rule: mini heads are found by name, portraits by the GUID folder that
///     tools/portraits/blur.ps1 fills, and wiki pages by slug.
/// </summary>
public static class CharacterUrlHelper
{
    private const string HeadImagePath = "/graphics/mini-heads";
    private const string PortraitPath = "/graphics/guessing";
    private const string WikiPath = "https://qsmp.fandom.com/wiki/";

    // One step per guess, matching the files blur.ps1 generates.
    private static readonly int[] PortraitBlurLevels = [200, 120, 75, 50, 30, 15];

    public static string GetHeadSrc(string characterName) => $"{HeadImagePath}/{characterName}.webp";

    public static string GetHeadSrc(Character character) => GetHeadSrc(character.Name);

    public static string GetHeadSrc(CharacterLookup characterLookup) => GetHeadSrc(characterLookup.Name);

    /// <summary>
    ///     Portrait folders are named without dashes, so this is the only valid string form of a portrait ID.
    /// </summary>
    public static string GetPortraitFolder(Guid portraitId) => portraitId.ToString("N");

    public static string GetPortraitSrc(string portraitFolder) => $"{PortraitPath}/{portraitFolder}/Head.webp";

    public static string GetBlurredPortraitSrc(string portraitFolder, int guessesMade)
    {
        var blur = guessesMade >= 0 && guessesMade < PortraitBlurLevels.Length
            ? PortraitBlurLevels[guessesMade]
            : PortraitBlurLevels[0];

        return $"{PortraitPath}/{portraitFolder}/Head-blur-{blur}.webp";
    }

    public static string GetWikiUrl(Character character) => WikiPath + character.WikiSlug;
}
