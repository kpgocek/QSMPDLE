namespace QSMPDLE.Web.Features.Gameplay.Services;

using Models;
using Stores;

/// <summary>
/// Orchestrates game flow: processing guesses, determining win/loss conditions, and updating stats.
/// </summary>
public interface IGameplayOrchestrator
{
    /// <summary>
    /// Processes a guess and returns game outcome information.
    /// </summary>
    GameplayResult ProcessGuess(Game game, int characterId, GameState state);

    /// <summary>
    /// Records a win in player statistics and returns updated stats.
    /// </summary>
    Task<PlayerStats> RecordWinAsync(Game game, bool endlessMode);

    /// <summary>
    /// Records a loss in player statistics and returns updated stats.
    /// </summary>
    Task<PlayerStats> RecordLossAsync();
}

/// <summary>
/// Result of processing a guess - contains win/loss state and stats.
/// </summary>
public record GameplayResult(
    Guess LatestGuess,
    bool IsGameWon,
    bool IsGameLost,
    bool StatsRecorded = false
);

public class GameplayOrchestrator : IGameplayOrchestrator
{
    private readonly IGameService _gameService;
    private readonly IPlayerStatsStore _playerStatsStore;

    public GameplayOrchestrator(IGameService gameService, IPlayerStatsStore playerStatsStore)
    {
        _gameService = gameService;
        _playerStatsStore = playerStatsStore;
    }

    /// <summary>
    /// Processes a guess, updates game state, and checks win/loss conditions.
    /// </summary>
    public GameplayResult ProcessGuess(Game game, int characterId, GameState state)
    {
        // Check if already guessed
        if (state.GuessesMade.Contains(characterId))
            return new GameplayResult(null!, false, false);

        // Submit guess through game service
        var latestGuess = _gameService.SubmitGuess(game, characterId);
        state.GuessesMade.Add(characterId);

        bool isGameWon = false;
        bool isGameLost = false;

        // Check win condition
        if (latestGuess.IsCorrect)
        {
            isGameWon = true;
            state.IsCompleted = true;
        }
        // Check loss condition (6 wrong guesses)
        else if (game.Guesses.Count >= 6)
        {
            isGameLost = true;
            state.IsFailed = true;
        }

        return new GameplayResult(latestGuess, isGameWon, isGameLost);
    }

    /// <summary>
    /// Records a win: updates games played, wins, streaks, and distribution.
    /// </summary>
    public async Task<PlayerStats> RecordWinAsync(Game game, bool endlessMode)
    {
        if (endlessMode)
            return new PlayerStats();

        var stats = await _playerStatsStore.LoadAsync();

        stats.GamesPlayed++;
        stats.GamesWon++;

        // Update streak
        if (stats.LastCompletedDayNumber == game.DayNumber - 1)
        {
            stats.CurrentStreak++;
        }
        else if (stats.LastCompletedDayNumber != game.DayNumber)
        {
            stats.CurrentStreak = 1;
        }

        stats.MaxStreak = Math.Max(stats.MaxStreak, stats.CurrentStreak);
        stats.LastCompletedDayNumber = game.DayNumber;

        // Update guess distribution (1-6 guesses)
        var guessCount = Math.Clamp(game.Guesses.Count, 1, 6);
        stats.GuessDistribution[guessCount - 1]++;

        await _playerStatsStore.SaveAsync(stats);
        return stats;
    }

    /// <summary>
    /// Records a loss: resets streak.
    /// </summary>
    public async Task<PlayerStats> RecordLossAsync()
    {
        var stats = await _playerStatsStore.LoadAsync();

        stats.GamesPlayed++;
        stats.CurrentStreak = 0;

        await _playerStatsStore.SaveAsync(stats);
        return stats;
    }
}
