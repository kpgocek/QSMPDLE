using System.Text.RegularExpressions;

namespace WikiScraper.Helpers;

public static class WikiTextHelper
{
    public static string CleanWikiText(this string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        // remove icons
        value = Regex.Replace(
            value,
            @"\[\[File:.*?\]\]",
            ""
        );

        // [https://url.com Text] -> Text
        value = Regex.Replace(
            value,
            @"\[(https?:\/\/[^\s\]]+)\s+([^\]]+)\]",
            "$2");

        // [https://url.com] -> ""
        value = Regex.Replace(
            value,
            @"\[https?:\/\/[^\]]+\]",
            "");

        // [[Target|Text]] -> Text
        value = Regex.Replace(
            value,
            @"\[\[[^|\]]+\|([^\]]+)\]\]",
            "$1");


        // {{Target|Text}} -> Text
        value = Regex.Replace(
            value,
            @"\{\{[^|\}]+\|([^\}]+)\}\}",
            "$1"
        );

        // [[Target]] -> Target
        value = Regex.Replace(
            value,
            @"\[\[([^\]]+)\]\]",
            "$1");

        // Remove Read more...
        value = Regex.Replace(
            value,
            "{{Cotop}}|{{Colow}}|{{Collapsetop}}|{{Collapselow}}",
            ""
        );

        // Remove refs
        value = Regex.Replace(
            value,
            "<ref.*?>.*?</ref>",
            "",
            RegexOptions.Singleline);

        // remove html tags
        value = Regex.Replace(
            value,
            "<[^>]+>",
            "");

        // remove clean urls
        value = Regex.Replace(
            value,
            @"https?:\/\/\S+",
            "");

        // remove asterisks
        value = Regex.Replace(
            value,
            @"\*",
            ""
        );

        // normalize spaces
        value = Regex.Replace(
            value,
            @"\s+",
            " ");

        return value.Trim();
    }

    public static string RemoveBracketComments(this string text)
    {
        text = Regex.Replace(
            text,
            @"\(.*\)",
            ""
        );

        return text.Trim();
    }

    public static string ToUrlPartial(this string text)
    {
        text = Regex.Replace(text, " ", "_");

        return text.Trim();
    }

    public static string[] SplitOnSlash(this string text)
    {
        var all = text.Split('/').Select(x => x.Trim()).ToArray();

        return all;
    }

    public static string[] SplitOnDash(this string text)
    {
        var all = text.Split('-').Select(x => x.Trim()).ToArray();

        return all;
    }
}
