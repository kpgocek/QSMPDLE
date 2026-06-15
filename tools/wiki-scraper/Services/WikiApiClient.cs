using System.Text.Json;

namespace WikiScraperQSMP.Services;

public sealed class WikiApiClient(HttpClient http)
{
    private const string ApiUrl = "https://qsmp.fandom.com/api.php";

    public async Task<string> GetPageContentAsync(string pageTitle)
    {
        var url =
            $"{ApiUrl}" +
            $"?action=query" +
            $"&titles={Uri.EscapeDataString(pageTitle)}" +
            $"&prop=revisions" +
            $"&rvprop=content" +
            $"&rvslots=main" +
            $"&format=json";

        return await http.GetStringAsync(url);
    }

    public async Task<string> GetMemberPageContent(string name) => await GetPageContentAsync(name);

    public async Task<string> GetQsmp2PageContent() => await GetPageContentAsync("QSMP_II");

    public async Task<string?> ResolveImageUrlAsync(string? iconName)
    {
        if (iconName is null) return iconName;

        var url =
            $"{ApiUrl}" +
            $"?action=query" +
            $"&titles=File:{Uri.EscapeDataString(iconName)}.png" +
            $"&prop=imageinfo" +
            $"&iiprop=url" +
            $"&format=json";

        var json =
            await http.GetStringAsync(url);

        using var doc = JsonDocument.Parse(json);

        return doc.RootElement
            .GetProperty("query")
            .GetProperty("pages")
            .EnumerateObject()
            .FirstOrDefault()
            .Value
            .GetProperty("imageinfo")[0]
            .GetProperty("url")
            .GetString();
    }
}
