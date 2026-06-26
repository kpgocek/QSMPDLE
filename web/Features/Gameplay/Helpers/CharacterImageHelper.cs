namespace QSMPDLE.Web.Features.Gameplay.Helpers;

using QSMPDLE.Web.Features.Gameplay.Models;

/// <summary>
/// Helper for generating character image URLs.
/// </summary>
public static class CharacterImageHelper
{
    private const string HeadImagePath = "/graphics/mini-heads";
    private const string Extension = ".webp";

    /// <summary>
    /// Gets the mini head image URL for a character.
    /// </summary>
    public static string GetHeadSrc(Character character) =>
        $"{HeadImagePath}/{character.Name}{Extension}";

    /// <summary>
    /// Gets the mini head image URL for a character lookup.
    /// </summary>
    public static string GetHeadSrc(CharacterLookup characterLookup) =>
        $"{HeadImagePath}/{characterLookup.Name}{Extension}";
}
