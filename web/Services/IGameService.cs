using QSMPDLE.Web.Data.Gameplay;

namespace QSMPDLE.Web.Services;

public interface IGameService
{
    Task<Game> StartDailyAsync(CancellationToken cancellationToken = default);
    Game StartEndless();
    Guess SubmitGuess(Game game, int memberId);
}
