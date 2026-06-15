namespace WikiScraperQSMP.Helpers;

public static class LinqHelper
{
    public static IEnumerable<string> SkipEmptyOrWhiteSpace(this IEnumerable<string> selection) => selection.Where(x => !string.IsNullOrWhiteSpace(x));
}
