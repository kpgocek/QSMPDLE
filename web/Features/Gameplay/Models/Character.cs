using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace QSMPDLE.Web.Features.Gameplay.Models;

public sealed class Character
{
    [Column("id")]
    [JsonIgnore]
    public int Id { get; init; }

    [Column("name")]
    [MaxLength(32)]
    public required string Name { get; init; }

    [Column("aliases")]
    public List<string> Aliases { get; init; } = [];

    [Column("pronouns")]
    public List<string> Pronouns { get; init; } = [];

    [Column("language_count")]
    public int LanguageCount { get; init; }

    [Column("affiliations")]
    public List<string> Affiliations { get; init; } = [];

    [Column("species")]
    public List<string> Species { get; init; } = [];

    /// <summary>
    /// Names the portrait folder; build paths with <see cref="Helpers.CharacterUrlHelper"/>.
    /// </summary>
    [Column("portrait_id")]
    [JsonIgnore]
    public Guid PortraitId { get; init; }

    [Column("join_day_number")]
    public int? JoinDayNumber { get; init; }

    /// <summary>
    /// The wiki page name, not the full URL; see <see cref="Helpers.CharacterUrlHelper.GetWikiUrl"/>
    /// </summary>
    [Column("wiki_slug")]
    [MaxLength(32)]
    public string WikiSlug { get; init; } = "";

    [Column("minecraft_username")]
    [MaxLength(32)]
    public string? MinecraftUsername { get; init; }

    [Column("phone_numbers")]
    public List<int> PhoneNumbers { get; init; } = [];

    [Column("youtube_subscribers")]
    public int? YouTubeSubscribers { get; init; }

    [Column("twitch_followers")]
    public int? TwitchFollowers { get; init; }

    [Column("kick_followers")]
    public int? KickFollowers { get; init; }

    [Column("won_award_2026")]
    public bool WonAward2026 { get; init; }
}
