using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using WikiScraperQSMP.Helpers;

namespace WikiScraperQSMP.Services;

public static class WikiPageParser
{
    private const string CharacterPageTemplateName = "Character";

    public static string? ExtractWikiText(string json)
    {
        using var doc = JsonDocument.Parse(json);

        var page = doc.RootElement
            .GetProperty("query")
            .GetProperty("pages")
            .EnumerateObject()
            .First()
            .Value;

        if (page.TryGetProperty("missing", out _))
        {
            return null;
        }

        if (!page.TryGetProperty("revisions", out var revisions))
        {
            return null;
        }

        var text = revisions[0]
            .GetProperty("slots")
            .GetProperty("main")
            .GetProperty("*")
            .GetString();

        File.AppendAllText(
            "temp.file",
            text);

        return text;
    }

    public static Dictionary<string, string> ParseCharacterPage(string wikiText)
    {
        var startMatch = Regex.Match(
            wikiText,
            $@"\{{\{{{Regex.Escape(CharacterPageTemplateName)}
                (?=\r?\n|\|)",
            RegexOptions.IgnorePatternWhitespace);

        if (!startMatch.Success)
            return [];

        var start = startMatch.Index;

        var end = FindTemplateEnd(
            wikiText,
            start);

        var template =
            wikiText[start..end];

        Dictionary<string, string> fields = [];

        string? currentKey = null;

        foreach (var rawLine in template.Split('\n'))
        {
            var line = rawLine.TrimEnd();

            if (line.StartsWith('|'))
            {
                var parts =
                    line[1..].Split('=', 2);

                if (parts.Length != 2)
                    continue;

                currentKey =
                    parts[0].Trim();

                fields[currentKey] =
                    parts[1].Trim();
            }
            else if (
                currentKey is not null &&
                !string.IsNullOrWhiteSpace(line))
            {
                fields[currentKey] +=
                    "\n" + line.Trim();
            }
        }

        return fields;
    }

    public static List<string> ParseWikiValues(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return value
            .Split('\n')
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.TrimStart('*').Trim())
            .Select(x => x.CleanWikiText())
            .Distinct()
            .ToList();
    }

    public static (string Link, string Name) ParseMinecraftUsername(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (string.Empty, string.Empty);

        var match = Regex.Match(
            value,
            @"\[(https?:\/\/[^\s\]]+)\s+([^\]]+)\]");

        if (!match.Success)
            return (string.Empty, value);

        return (
            match.Groups[1].Value,
            match.Groups[2].Value
        );
    }

    public static List<string> ParseAliases(string? value)
    {
        var result = ParseWikiValues(value);

        return result
            .Select(a => Regex.Replace(a, @"\s*\([^)]*\)", ""))
            .Select(a => Regex.Replace(a, @"\.\s.*$", ""))
            .ToList();
    }

    public static (DateOnly? Date, int? Day) ParseJoinDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (null, null);

        var parts = value.SplitOnDash();

        DateOnly? date = null;

        if (DateOnly.TryParseExact(
                parts[0].NoOrdinalSuffixes().Trim(),
                "MMMM d, yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDate))
        {
            date = parsedDate;
        }

        int? day = null;

        if (parts.Length > 1)
        {
            var match = Regex.Match(
                parts[1],
                @"Day\s*(\d+)",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                day = int.Parse(match.Groups[1].Value);
            }
        }

        // Infer missing day from date
        if (date is not null && day is null)
        {
            DateOnly qsmp2StartDate = new(2026, 3, 14);

            day = date.Value.DayNumber
                - qsmp2StartDate.DayNumber
                + 1;
        }

        return (date, day);
    }

    private static int FindTemplateEnd(
        string wikiText,
        int start)
    {
        var depth = 0;

        for (var i = start; i < wikiText.Length - 1; i++)
        {
            if (wikiText[i] == '{'
                && wikiText[i + 1] == '{')
            {
                depth++;
                i++;
                continue;
            }

            if (wikiText[i] == '}'
                && wikiText[i + 1] == '}')
            {
                depth--;
                i++;

                if (depth == 0)
                    return i + 1;
            }
        }

        throw new InvalidOperationException(
            "Template end not found.");
    }
}
