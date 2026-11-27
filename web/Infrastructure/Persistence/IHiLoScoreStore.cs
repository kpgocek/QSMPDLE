namespace QSMPDLE.Web.Infrastructure.Persistence;

public interface IHiLoScoreStore
{
    /// <summary>The best score so far, or 0 before the first.</summary>
    Task<int> GetBestScoreAsync(Guid playerId, CancellationToken cancellationToken = default);

    /// <summary>Keeps the score only when it beats the best one.</summary>
    Task RecordScoreAsync(Guid playerId, int score, CancellationToken cancellationToken = default);
}
