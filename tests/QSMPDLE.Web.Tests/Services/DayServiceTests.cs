using QSMPDLE.Web.Features.Gameplay.Services;

namespace QSMPDLE.Web.Tests.Services;

public sealed class DayServiceTests
{
    [Fact]
    public void GetYesterdayArchiveLink_ReturnsArchiveDayLink()
    {
        var service = new DayService();

        var link = service.GetYesterdayArchiveLink();

        Assert.Matches("^/archive/day/\\d+$", link);
    }

    [Fact]
    public void GetArchiveDayNumber_ConvertsDateUsingFirstDay()
    {
        var service = new DayService();
        var firstDay = service.GetFirstDay();

        var archiveDay = service.GetArchiveDayNumber(firstDay.AddDays(4));

        Assert.Equal(5, archiveDay);
    }
}
