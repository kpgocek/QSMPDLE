using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WikiScraper.Models;

public sealed class Member
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    [JsonIgnore]
    public int Id { get; set; }

    public required string Name { get; set; }

    [JsonIgnore]
    public string AliasesJson { get; set; } = "";

    [JsonIgnore]
    public string PronounsJson { get; set; } = "";

    public int Languages { get; set; }

    [JsonIgnore]
    public string AffiliationsJson { get; set; } = "";

    [JsonIgnore]
    public string SpeciesJson { get; set; } = "";

    [JsonIgnore]
    public string CharacterIconUrl { get; set; } = "";

    public int? JoinDayNumber { get; set; }

    public string MemberPageUrl { get; set; } = "";

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
        get => CharacterIconUrl;
        set => CharacterIconUrl = value ?? "";
    }

    private static List<string> DeserializeList(string json) =>
        string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];

    private static string SerializeList(IEnumerable<string>? values) =>
        JsonSerializer.Serialize(values ?? [], JsonOptions);
}
