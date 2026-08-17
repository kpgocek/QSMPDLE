namespace QSMPDLE.Web.Services
{
    // Represents player's status for a given archive day
    public enum DayStatus
    {
        NotStarted = 0,
        InProgress = 1,
        Won = 2,
        Lost = 3
    }

    public static class DayStatusExtensions
    {
        public static DayStatus MergeWith(this DayStatus current, DayStatus fallback)
        {
            return current switch
            {
                DayStatus.InProgress => DayStatus.InProgress,
                DayStatus.Won => DayStatus.Won,
                DayStatus.Lost => DayStatus.Lost,
                _ => fallback
            };
        }
    }
}
