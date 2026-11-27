using QSMPDLE.Web.Features.HiLo.Models;

namespace QSMPDLE.Web.Features.HiLo.Services;

public interface IHiLoService
{
    /// <summary>Carries the previous round's hidden character over as the known one; without a previous round, starts a run.</summary>
    Task<HiLoRound> CreateRoundAsync(HiLoRound? previous = null, CancellationToken cancellationToken = default);

    Task<int> GetBestScoreAsync(CancellationToken cancellationToken = default);
    Task RecordScoreAsync(int score, CancellationToken cancellationToken = default);
}
