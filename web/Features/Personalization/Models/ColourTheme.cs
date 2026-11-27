namespace QSMPDLE.Web.Features.Personalization.Models;

/// <summary>A theme the player can pick. <see cref="Id" /> names its <c>[data-theme]</c> block in wwwroot/css/themes.css.</summary>
public sealed record ColourTheme(string Id, string Name)
{
    private static readonly ColourTheme Classic = new("classic", "Classic");
    private static readonly ColourTheme Retro = new("retro", "Retro");
    private static readonly ColourTheme Grayscale = new("grayscale", "Grayscale");
    private static readonly ColourTheme Colmena = new("colmena", "Bee");
    private static readonly ColourTheme YoungMulti = new("youngmulti", "Young");
    private static readonly ColourTheme Ashswag = new("ashswag", "Swag");
    private static readonly ColourTheme Mike = new("mike", "Mikey");
    private static readonly ColourTheme Katie = new("katie", "Katie");

    public static readonly ColourTheme Default = Classic;

    public static readonly IReadOnlyList<ColourTheme> All =
    [
        Default,
        Retro,
        Grayscale,
        Colmena,
        YoungMulti,
        Ashswag,
        Mike,
        Katie
    ];

    /// <summary>The saved theme, or <see cref="Default" /> when none is saved or it was removed.</summary>
    public static ColourTheme Find(string? id) => All.FirstOrDefault(theme => theme.Id == id) ?? Default;
}
