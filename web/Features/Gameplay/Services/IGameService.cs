using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.Gameplay.Services;

public interface IGameService
{
    Task<GameState> StartDailyAsync(CancellationToken cancellationToken = default);
    Task<GameState> StartPracticeAsync(CancellationToken cancellationToken = default);
    Task<GameState> StartArchivalAsync(int dayNumber, CancellationToken cancellationToken = default);

    // ENDPOINTS
    Task<int> GetTodayDayNumberAsync(CancellationToken cancellationToken);
}
