namespace QSMPDLE.Web.Features.Gameplay.Services;

public sealed class DayService : IDayService
{
    private static readonly DateOnly FirstDay = new(2026, 6, 15);

    public DateOnly GetFirstDay() => FirstDay;

    public int GetTodayDayNumber()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return GetArchiveDayNumber(today);
    }

    public int GetMaxArchiveDay() => Math.Max(1, GetTodayDayNumber() - 1);

    public int GetArchiveDayNumber(DateOnly date) => date.DayNumber - FirstDay.DayNumber + 1;

    public DateOnly GetArchiveDate(int dayNumber) => FirstDay.AddDays(dayNumber - 1);

    public int GetYesterdayArchiveDayNumber() => Math.Max(1, GetTodayDayNumber() - 1);

    public string GetYesterdayArchiveLink() => $"/archive/day/{GetYesterdayArchiveDayNumber()}";
}