using FluentAssertions;

namespace QSMPDLE.Web.Tests.Services;

public sealed class PublicFilesTests
{
    [Fact]
    public void WebRoot_PublicFolder_HasNoNameListsOrScripts()
    {
        // Everything in wwwroot is served; mapping.csv once told visitors the daily answer and unreleased characters.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QSMPDLE.sln")))
            directory = directory.Parent;

        var files = Directory.EnumerateFiles(Path.Combine(directory!.FullName, "web", "wwwroot"), "*", SearchOption.AllDirectories)
            .Select(file => Path.GetExtension(file).ToLowerInvariant());

        files.Should().NotBeEmpty().And.NotContain([".csv", ".ps1", ".json", ".md"]);
    }
}
