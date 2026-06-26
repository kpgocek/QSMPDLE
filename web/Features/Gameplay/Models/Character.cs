using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QSMPDLE.Web.Features.Gameplay.Models;

public sealed class Character
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    [Column("id")]
    [JsonIgnore]
    public int Id { get; set; }

    [Column("name")]
    public required string Name { get; set; }

    [Column("aliases")]
    [JsonIgnore]
    public string AliasesJson { get; set; } = "";

    [Column("pronouns")]
    [JsonIgnore]
    public string PronounsJson { get; set; } = "";

    [Column("languages")]
    public int Languages { get; set; }

    [Column("affiliations")]
    [JsonIgnore]
    public string AffiliationsJson { get; set; } = "";

    [Column("species")]
    [JsonIgnore]
    public string SpeciesJson { get; set; } = "";

    [Column("icon_url")]
    [JsonIgnore]
    public string IconUrl { get; set; } = "";

    [Column("join_day_number")]
    public int? JoinDayNumber { get; set; }

    [Column("page_url")]
    public string PageUrl { get; set; } = "";

    [Column("minecraft_username")]
    public string? MinecraftUsername { get; set; }

    [NotMapped]
    public List<string> Aliases
    {
        get => DeserializeList(AliasesJson);
        set => AliasesJson = SerializeList(value);
    }

    [NotMapped]
    public List<string> Pronouns
    {
        get => DeserializeList(PronounsJson);
        set => PronounsJson = SerializeList(value);
    }

    [NotMapped]
    public List<string> Affiliations
    {
        get => DeserializeList(AffiliationsJson);
        set => AffiliationsJson = SerializeList(value);
    }

    [NotMapped]
    public List<string> Species
    {
        get => DeserializeList(SpeciesJson);
        set => SpeciesJson = SerializeList(value);
    }

    [NotMapped]
    public string? CharacterIcon
    {
        get => IconUrl;
        set => IconUrl = value ?? "";
    }

    private static List<string> DeserializeList(string json) =>
        string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];

    private static string SerializeList(IEnumerable<string>? values) =>
        JsonSerializer.Serialize(values ?? [], JsonOptions);
}
