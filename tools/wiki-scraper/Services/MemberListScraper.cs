using System.Text.Json;
using System.Text.RegularExpressions;
using WikiScraperQSMP.Helpers;

namespace WikiScraperQSMP.Services;

public sealed class MemberListScraper(WikiApiClient api)
{
    public async Task<List<string>> GetMembers()
    {
        var excludedMembers = JsonSerializer.Deserialize<List<string>>(File.ReadAllText("excluded.json"))!;

        var json =
            await api.GetQsmp2PageContent();

        var wikiText =
            WikiPageParser.ExtractWikiText(json);

        if (wikiText is null) return [];

        var start =
            wikiText.IndexOf(
                "== Members ==",
                StringComparison.Ordinal);

        var end =
            wikiText.IndexOf(
                "==Events==",
                StringComparison.Ordinal);

        if (start < 0 || end < 0)
            throw new Exception(
                "Members section not found.");

        var membersSection =
            wikiText[start..end];

        return Regex.Matches(
                membersSection,
                @"\[\[([^|\]]+)(?:\|[^\]]+)?\]\]")
            .Select(x => x.Groups[1].Value.Trim())
            .Distinct()
            .Where(x => !excludedMembers.Contains(x))
            .OrderBy(x => x)
            .ToList();
    }

    private static string ExtractMembersSection(
        string wikiText)
    {
        var start =
            wikiText.IndexOf(
                "== Members ==",
                StringComparison.OrdinalIgnoreCase);

        if (start < 0)
            throw new Exception(
                "Members section not found.");

        var end =
            wikiText.IndexOf(
                "\n==",
                start + 1,
                StringComparison.OrdinalIgnoreCase);

        if (end < 0)
            end = wikiText.Length;

        return wikiText[start..end];
    }
}
