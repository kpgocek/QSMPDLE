using QSMPDLE.Web.Data.Gameplay;

namespace QSMPDLE.Web.Services;

public interface IShareTextBuilder
{
    string BuildDailyResult(
        int dayNumber,
        IReadOnlyList<Guess> guesses);

    string BuildDailyChallenge(
        int dayNumber
    );
}
