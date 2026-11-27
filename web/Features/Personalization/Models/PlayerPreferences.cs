using System.Text.Json.Serialization;

namespace QSMPDLE.Web.Features.Personalization.Models;

/// <summary>
/// Kept only in the browser and never sent to the database. The script at the top of App.razor reads this
/// JSON before the first paint, so the property names and enum spellings are fixed.
/// </summary>
public sealed record PlayerPreferences
{
    public const int MaxNameLength = 20;

    [JsonPropertyName("motion")]
    [JsonConverter(typeof(JsonStringEnumConverter<MotionPreference>))]
    public MotionPreference Motion { get; init; }

    /// <summary>Shown on the Profile page and used in share text in place of "I".</summary>
    [JsonPropertyName("name")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("avatar")]
    public int? AvatarCharacterId { get; init; }

    /// <summary>A <see cref="ColourTheme"/> id; null is the default theme.</summary>
    [JsonPropertyName("theme")]
    public string? Theme { get; init; }

    /// <summary>One line of at most <see cref="MaxNameLength"/> characters, or null when nothing is left.</summary>
    public static string? CleanName(string? name)
    {
        if (name is null)
            return null;

        // Control characters would break the share text's lines; runs of spaces collapse to one.
        var words = new string([.. name.Select(c => char.IsControl(c) ? ' ' : c)])
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cleaned = string.Join(' ', words);
        if (cleaned.Length > MaxNameLength)
            cleaned = cleaned[..MaxNameLength].TrimEnd();

        return cleaned.Length == 0 ? null : cleaned;
    }
}
