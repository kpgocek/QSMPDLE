using FluentAssertions;
using QSMPDLE.Web.Features.Personalization.Models;

namespace QSMPDLE.Web.Tests.Services;

public sealed class PlayerPreferencesTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("  Krzysztof  ", "Krzysztof")]
    [InlineData("Big\r\nTubbo\tfan", "Big Tubbo fan")]
    [InlineData("A name that is much too long", "A name that is much")]
    public void CleanName_TypedName_IsOneShortTrimmedLine(string? typed, string? expected)
    {
        PlayerPreferences.CleanName(typed).Should().Be(expected);
    }
}
