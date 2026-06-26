using System.Text.RegularExpressions;

namespace WikiScraper.Helpers;

public static class StringHelper
{
    public static string DigitsOnly(this string text)
    {
        text = Regex.Replace(text, @"\D", "");

        return text.Trim();
    }

    public static string AlphanumericOnly(this string text)
    {
        text = string.Join("", Regex.Matches(text, @"[A-Za-z]|[^\u0000-\u007F]"));

        return text.Trim();
    }

    public static string NoOrdinalSuffixes(this string value)
    {
        value = Regex.Replace(
            value,
            @"(\d+)(st|nd|rd|th)",
            "$1",
            RegexOptions.IgnoreCase);

        return value.Trim();
    }

    public static string CapitalLettersOnly(this string value)
    {
        value = Regex.Replace(
            value,
            "[^A-Z ]",
            ""
        );

        return value.Trim();
    }
}
