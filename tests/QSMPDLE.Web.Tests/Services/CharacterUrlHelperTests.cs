using FluentAssertions;
using QSMPDLE.Web.Features.Gameplay.Helpers;
using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Tests.Services;

public sealed class CharacterUrlHelperTests
{
    private const string Folder = "343a2bcfa31a4cb79ebb12ab624d0d09";

    [Fact]
    public void GetPortraitFolder_Guid_MatchesDashlessFolderName()
    {
        var folder = CharacterUrlHelper.GetPortraitFolder(Guid.Parse("343a2bcf-a31a-4cb7-9ebb-12ab624d0d09"));

        folder.Should().Be(Folder);
    }

    [Theory]
    [InlineData(0, 200)]
    [InlineData(1, 120)]
    [InlineData(5, 15)]
    [InlineData(6, 200)]
    [InlineData(-1, 200)]
    public void GetBlurredPortraitSrc_GuessesMade_PicksBlurStep(int guessesMade, int blur)
    {
        var src = CharacterUrlHelper.GetBlurredPortraitSrc(Folder, guessesMade);

        src.Should().Be($"/graphics/guessing/{Folder}/Head-blur-{blur}.webp");
    }

    [Fact]
    public void GetPortraitSrc_Folder_PointsAtUnblurredHead() => CharacterUrlHelper.GetPortraitSrc(Folder).Should()
        .Be($"/graphics/guessing/{Folder}/Head.webp");

    [Fact]
    public void GetHeadSrc_Character_UsesName() => CharacterUrlHelper.GetHeadSrc(new Character { Name = "FitMC" })
        .Should().Be("/graphics/mini-heads/FitMC.webp");

    [Fact]
    public void GetWikiUrl_Character_PrefixesSlug()
    {
        var character = new Character { Name = "YoungMulti", WikiSlug = "YoungMulti" };

        CharacterUrlHelper.GetWikiUrl(character).Should().Be("https://qsmp.fandom.com/wiki/YoungMulti");
    }
}
