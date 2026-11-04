using QSMPDLE.Web.Features.Communication.GameEvents;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Infrastructure.LocalStorage;
using QSMPDLE.Web.Infrastructure.Persistence;

namespace QSMPDLE.Web.Features.Statistics.Services;

/// <summary>
/// Persists session events only. Player statistics are projected from the stored sessions
/// (<see cref="PlayerStatsProjection"/>), so recording events never reloads a player's history.
/// </summary>
public sealed class StatisticsService(IPlayerStatsStore PlayerStatsStore, IGameStatsStore GameStatsStore) : IStatisticsService
{
    public async Task RecordGameStartedAsync(GameStartedEvent eventData)
    {
        EnsureValidPlayerId(eventData.PlayerId);

        var gameStats = await GetGameStatsAsync(eventData.GameId);

        ArgumentNullException.ThrowIfNull(gameStats);

        gameStats.PlayerId = eventData.PlayerId;
        gameStats.StartedOnUtc = eventData.Timestamp;
        gameStats.DailyNumber = eventData.DayNumber;
        gameStats.TargetCharacterId = eventData.TargetCharacterId;
        gameStats.PuzzleId = eventData.PuzzleId;
        gameStats.SessionCategory = eventData.SessionCategory;
        gameStats.FirstEntryPoint = eventData.EntryPoint;
        gameStats.Mode = eventData.GameMode;

        await SaveGameStatsAsync(gameStats);
    }

    public async Task RecordGuessMadeAsync(GuessMadeEvent eventData)
    {
        EnsureValidPlayerId(eventData.PlayerId);

        await GameStatsStore.AddGuessAsync(eventData.GameId, eventData.PlayerId, eventData.GuessedCharacterId);
    }

    public async Task RecordGameFinishedAsync(GameFinishedEvent eventData)
    {
        EnsureValidPlayerId(eventData.PlayerId);

        await GameStatsStore.MarkFinishedAsync(eventData.GameId, eventData.PlayerId, eventData.Timestamp, eventData.IsWon);
    }

    public async Task<GameSession> GetGameStatsAsync(Guid gameId) => await GameStatsStore.LoadOrNewAsync(gameId);
    public async Task<GameSession?> GetActiveCanonicalSessionAsync(Guid playerId, int puzzleId) => await GameStatsStore.GetActiveCanonicalSessionAsync(playerId, puzzleId);
    public async Task<GameSession> ClaimCanonicalSessionAsync(GameSession proposedSession) => await GameStatsStore.ClaimCanonicalSessionAsync(proposedSession);
    public async Task<List<int>> GetPlayerCompletedDailyNumbersAsync(Guid playerId) => await GameStatsStore.GetPlayerCompletedDailyNumbersAsync(playerId);
    private async Task SaveGameStatsAsync(GameSession gameStats) => await GameStatsStore.SaveAsync(gameStats);

    public async Task<PlayerStats> GetPlayerStatsAsync() => await PlayerStatsStore.LoadAsync();
    public Task<Guid> GetPlayerIdAsync() => PlayerStatsStore.GetPlayerIdAsync();

    private static void EnsureValidPlayerId(Guid playerId)
    {
        if (playerId == Guid.Empty)
        {
            throw new InvalidOperationException("Cannot record telemetry without a valid player id.");
        }
    }
}
