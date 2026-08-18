namespace QSMPDLE.Web.Features.Gameplay.Services;

public interface IDayService
{
    DateOnly GetFirstDay();
    int GetTodayDayNumber();
    int GetMaxArchiveDay();
    int GetArchiveDayNumber(DateOnly date);
    DateOnly GetArchiveDate(int dayNumber);
    int GetYesterdayArchiveDayNumber();
    string GetYesterdayArchiveLink();
}