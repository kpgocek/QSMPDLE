using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.Gameplay.Services;

public interface ICharacterComparer
{
    Task<GuessResult> CompareAsync(int targetId, int guessedId, CancellationToken cancellationToken = default);
}