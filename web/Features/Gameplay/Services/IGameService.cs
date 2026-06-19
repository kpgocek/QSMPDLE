using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Models;

namespace QSMPDLE.Web.Features.Gameplay.Services;

public interface IGameService
{
    Task<Game> StartDailyAsync(CancellationToken cancellationToken = default);
    Task<Character> GetDailyCharacterAsync(CancellationToken cancellationToken = default);
    Task<int> GetDayAsync(CancellationToken cancellationToken = default);
    Task<Game> StartEndlessAsync();
    Guess SubmitGuess(Game game, int characterId);
}
