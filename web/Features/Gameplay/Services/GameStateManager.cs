namespace QSMPDLE.Web.Features.Gameplay.Services;

using Models;
using Stores;

/// <summary>
/// Manages game state loading, saving, and lifecycle for both daily and endless modes.
/// </summary>
public interface IGameStateManager
{
    Task<GameState> LoadOrCreateAsync(bool endlessMode, string gameKey);
    Task SaveAsync(GameState state, bool endlessMode, string gameKey);
    void ClearGuessField();
}

public class GameStateManager : IGameStateManager
{
    private readonly IGameStateStore _gameStateStore;

    public GameStateManager(IGameStateStore gameStateStore)
    {
        _gameStateStore = gameStateStore;
    }

    /// <summary>
    /// Loads existing game state for daily mode or creates new state for endless mode.
    /// </summary>
    public async Task<GameState> LoadOrCreateAsync(bool endlessMode, string gameKey)
    {
        if (endlessMode)
            return new GameState();

        await _gameStateStore.Init(gameKey);
        return await _gameStateStore.GetAsync() ?? new GameState();
    }

    /// <summary>
    /// Persists game state to local storage (daily mode only).
    /// </summary>
    public async Task SaveAsync(GameState state, bool endlessMode, string gameKey)
    {
        if (!endlessMode)
            await _gameStateStore.SaveAsync(state);
    }

    public void ClearGuessField()
    {
        // Placeholder for any cleanup logic if needed
    }
}
