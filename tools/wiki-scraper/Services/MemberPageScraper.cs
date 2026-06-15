using System.Text.Json;
using System.Text.RegularExpressions;
using WikiScraperQSMP.Helpers;
using WikiScraperQSMP.Models;

namespace WikiScraperQSMP.Services;

public sealed class MemberPageScraper(WikiApiClient api)
{
    private const string WikiUrl = "https://qsmp.fandom.com/wiki/";

    public async Task<Member?> Scrape(string username)
    {
        var overviewJson = await api.GetPageContentAsync(username);
        var season2Json = await api.GetPageContentAsync($"{username}/Season_2");

        var overviewWiki = WikiPageParser.ExtractWikiText(overviewJson);

        var season2Wiki = WikiPageParser.ExtractWikiText(season2Json);

        if (overviewWiki is null) return null;
        if (season2Wiki is null) return null;

        var overview =
            WikiPageParser.ParseCharacterPage(overviewWiki);

        var season2 =
            WikiPageParser.ParseCharacterPage(season2Wiki);

        var (_, joinDayNumber) =
            WikiPageParser.ParseJoinDate(
                season2.GetValueOrDefault(
                    "Join_Date"));

        var title = overview.GetValueOrDefault("Title");

        return new Member
        {
            Name = ResolveName(overview.GetValueOrDefault("Title"), username),

            CharacterIcon = await ResolveIconUrl(season2.GetValueOrDefault("Icon"), season2Wiki, overviewWiki),

            Pronouns =
                WikiPageParser.ParseWikiValues(
                    overview.GetValueOrDefault("Pronouns")
                )
                .Distinct()
                .Select(ResolvePronouns)
                .SkipEmptyOrWhiteSpace()
                .ToList(),

            Languages =
                ParseLanguages(
                    overview.GetValueOrDefault("Language(s)")
                )
                .Distinct()
                .SkipEmptyOrWhiteSpace()
                .Count(),

            MinecraftUsername =
                WikiPageParser.ParseMinecraftUsername(
                    overview.GetValueOrDefault("Username")
                ).Name,

            Aliases = new List<string>() { }
                .Concat(
                    WikiPageParser.ParseAliases(
                        overview.GetValueOrDefault("Aliases")
                    )
                )
                .Concat(
                    WikiPageParser.ParseAliases(
                        season2.GetValueOrDefault("Aliases")
                    )
                )
                .SelectMany(x => x.SplitOnSlash())
                .Distinct()
                .SkipEmptyOrWhiteSpace()
                .ToList(),

            Species =
                WikiPageParser.ParseWikiValues(
                    season2.GetValueOrDefault("Species")
                )
                .Select(s => s.RemoveBracketComments())
                .SelectMany(s => s.SplitOnDash())
                .SelectMany(s => s.SplitOnSlash())
                .Select(s => s.AlphanumericOnly())
                .Distinct()
                .SkipEmptyOrWhiteSpace()
                .ToList(),

            Affiliations =
                WikiPageParser.ParseWikiValues(
                    season2.GetValueOrDefault("Present_Affiliations")
                )
                .Concat(
                    WikiPageParser.ParseWikiValues(
                        season2.GetValueOrDefault("Groups")
                    )
                )
                .Select(s => s.RemoveBracketComments())
                .Distinct()
                .SkipEmptyOrWhiteSpace()
                .ToList(),

            JoinDayNumber = joinDayNumber,

            MemberPageUrl = $"{WikiUrl}{username.ToUrlPartial()}",
        };
    }

    private async Task<string?> ResolveIconUrl(string? iconName, params string[] wikiTexts)
    {
        if (!IsValidIconName(iconName)) iconName = ExtractIconFromAspects(wikiTexts);

        if (string.IsNullOrWhiteSpace(iconName)) throw new InvalidOperationException("No Icon Found");

        return await api.ResolveImageUrlAsync(iconName);
    }

    private static bool IsValidIconName(string? iconName) => !string.IsNullOrWhiteSpace(iconName) && iconName.Contains(".Icon");

    private static string? ExtractIconFromAspects(params string[] wikiTexts)
    {
        foreach (var text in wikiTexts)
        {
            var defaultMatch = Regex.Match(text, @"\|name\s+(\d+)\s*=\s*Default\b");

            if (!defaultMatch.Success)
                continue;

            var index =
                defaultMatch.Groups[1].Value;

            var iconMatch = Regex.Match(
                text,
                $@"\|icon\s+{Regex.Escape(index)}\s*=\s*([^\r\n|}}]+)");

            if (!iconMatch.Success) continue;

            var iconName = iconMatch.Groups[1].Value.Trim();

            if (!IsValidIconName(iconName)) continue;

            return iconName;
        }

        return null;
    }

    private static string ResolveName(string? title, string username)
    {
        if (string.IsNullOrWhiteSpace(title))
            return username;

        if (title.Contains("{{"))
            return username;

        return title;
    }

    public static string ResolvePronouns(string text)
    {
        if (Regex.IsMatch(text, @"\bany\b", RegexOptions.IgnoreCase))
            return "Any";

        return string.Join(
            ", ",
            Regex.Matches(text, @"\b[A-Za-z]+/[A-Za-z]+\b")
                .Select(x => x.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static readonly Dictionary<string, string[]> LanguageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["US ENG"] = ["ENG"],
        ["UK ENG"] = ["ENG"],
        ["US"] = ["ENG"],
        ["UK"] = ["ENG"],

        ["MX ESP"] = ["ESP"],
        ["MX"] = ["ESP"],

        ["COL"] = ["ESP"],
        ["AR"] = ["ESP"],
        ["PR"] = ["ESP"],

        ["BR MX"] = ["BR", "ESP"],
        ["BR CL"] = ["BR", "ESP"],

        ["ESP CA"] = ["CA"],
    };

    public static List<string> ParseLanguages(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        var languages = Regex.Matches(
                value,
                @"\{\{Language\|([^|}]+)")
            .Select(x => x.Groups[1].Value.Trim())
            .Distinct();

        return NormalizeLanguages(languages).ToList();
    }

    private static IReadOnlyCollection<string> NormalizeLanguages(IEnumerable<string> languages)
    {
        return languages
            .SelectMany(ExpandLanguage)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> ExpandLanguage(string language)
    {
        return LanguageMap.TryGetValue(language, out var mapped)
            ? mapped
            : [language];
    }
}
