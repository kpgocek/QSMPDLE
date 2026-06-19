using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.Sharing.Builders;

public interface IShareTextBuilder
{
    string BuildDailyResult(
        int dayNumber,
        IReadOnlyList<Guess> guesses);

    string BuildDailyChallenge(
        int dayNumber
    );
}
