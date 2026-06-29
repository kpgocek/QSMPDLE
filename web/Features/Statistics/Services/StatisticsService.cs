using QSMPDLE.Web.Features.Communication.GameEvents;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Infrastructure.LocalStorage;
using QSMPDLE.Web.Infrastructure.Persistence;

namespace QSMPDLE.Web.Features.Statistics.Services;

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
        gameStats.Mode = eventData.GameMode;

        if (eventData.GameMode == GameMode.Daily)
        {
            var playerStats = await GetPlayerStatsAsync();

            ArgumentNullException.ThrowIfNull(playerStats);

            playerStats.LastPlayedDailyGameId = eventData.GameId;

            await SavePlayerStatsAsync(playerStats);
        }

        await SaveGameStatsAsync(gameStats);
    }

    public async Task RecordGuessMadeAsync(GuessMadeEvent eventData)
    {
        EnsureValidPlayerId(eventData.PlayerId);

        var gameStats = await GetGameStatsAsync(eventData.GameId);

        ArgumentNullException.ThrowIfNull(gameStats);

        gameStats.PlayerId = eventData.PlayerId;
        gameStats.AddGuess(eventData.GuessedCharacterId);

        await SaveGameStatsAsync(gameStats);
    }
    public async Task RecordGameFinishedAsync(GameFinishedEvent eventData)
    {
        EnsureValidPlayerId(eventData.PlayerId);

        var gameStats = await GetGameStatsAsync(eventData.GameId);

        ArgumentNullException.ThrowIfNull(gameStats);

        gameStats.PlayerId = eventData.PlayerId;
        gameStats.FinishedOnUtc = eventData.Timestamp;
        gameStats.IsWon = eventData.IsWon;

        await SaveGameStatsAsync(gameStats);

        if (eventData.GameMode == GameMode.Daily)
        {
            var dayNumber = eventData.DayNumber
                ?? throw new InvalidOperationException("Cannot record daily statistics without a day number.");

            var playerStats = await GetPlayerStatsAsync();

            ArgumentNullException.ThrowIfNull(playerStats);

            playerStats.GamesPlayed++;
            playerStats.GuessDistribution[eventData.GuessCount - 1]++;

            if (eventData.IsWon)
            {
                playerStats.GamesWon++;

                if (playerStats.LastCompletedDayNumber is null || dayNumber - playerStats.LastCompletedDayNumber == 1)
                {
                    playerStats.CurrentStreak++;
                }
                else if (dayNumber != playerStats.LastCompletedDayNumber)
                {
                    playerStats.CurrentStreak = 1;
                }

                playerStats.LastCompletedDayNumber = dayNumber;
            }
            else
            {
                playerStats.CurrentStreak = 0;
            }

            if (playerStats.CurrentStreak > playerStats.MaxStreak)
            {
                playerStats.MaxStreak = playerStats.CurrentStreak;
            }

            await SavePlayerStatsAsync(playerStats);
        }
    }

    public async Task<GameSession> GetGameStatsAsync(Guid gameId) => await GameStatsStore.LoadOrNewAsync(gameId);
    private async Task SaveGameStatsAsync(GameSession gameStats) => await GameStatsStore.SaveAsync(gameStats);

    public async Task<PlayerStats> GetPlayerStatsAsync() => await PlayerStatsStore.LoadAsync();
    private async Task SavePlayerStatsAsync(PlayerStats playerStats) => await PlayerStatsStore.SaveAsync(playerStats);

    private static void EnsureValidPlayerId(Guid playerId)
    {
        if (playerId == Guid.Empty)
        {
            throw new InvalidOperationException("Cannot record telemetry without a valid player id.");
        }
    }
}
