using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.HiLo.Models;

public enum HiLoStat
{
    DayArrived,
    QPhoneNumber,
    YouTubeSubscribers,
    TwitchFollowers,
    KickFollowers
}

public static class HiLoStatExtensions
{
    /// <summary>The value to compare, or null without one; zero stands for an unused account.</summary>
    public static int? GetValue(this HiLoStat stat, Character character)
    {
        var value = stat switch
        {
            HiLoStat.DayArrived => character.JoinDayNumber,
            HiLoStat.QPhoneNumber => character.PhoneNumber,
            HiLoStat.YouTubeSubscribers => character.YouTubeSubscribers,
            HiLoStat.TwitchFollowers => character.TwitchFollowers,
            HiLoStat.KickFollowers => character.KickFollowers,
            _ => throw new ArgumentOutOfRangeException(nameof(stat))
        };

        return value > 0 ? value : null;
    }
}
