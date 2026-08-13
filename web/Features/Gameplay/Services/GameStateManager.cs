namespace QSMPDLE.Web.Features.Gameplay.Services;

using Models;
using QSMPDLE.Web.Extensions;
using QSMPDLE.Web.Features.Communication.GameEvents;
using QSMPDLE.Web.Infrastructure.LocalStorage;
using QSMPDLE.Web.Infrastructure.Persistence;
using QSMPDLE.Web.Features.Statistics.Services;

// <summary>
// Manages the game state for different game modes (daily, practice, archival).
// </summary>
public class GameStateManager(IGameStateStore GameStateStore, IGameService GameService, IPlayerStatsStore PlayerStatsStore,
    ICharacterStore CharacterStore, ICharacterComparer CharacterComparer, IStatisticsService StatisticsService) : IGameStateManager
{
    private const int MaxGuesses = 6;

    public GameState GameState { get; private set; } = null!;

    public async Task StartNewPracticeGameAsync(CancellationToken cancellationToken = default)
    {
        var playerId = await GetPlayerIdAsync();

        GameState = await GameService.StartPracticeAsync(cancellationToken);
        GameState.PlayerId = playerId;

        await GameStateStore.SaveAsync(GameState);
    }

    public async Task StartNewArchivedGameAsync(int dayNumber, CancellationToken cancellationToken = default)
    {
        var playerId = await GetPlayerIdAsync();

        GameState = await GameService.StartArchivalAsync(dayNumber, cancellationToken);
        GameState.PlayerId = playerId;

        await GameStateStore.SaveAsync(GameState);
    }

    public async Task<LoadGameResult> StartGameAsync(GameMode mode, int? dayNumber = null, CancellationToken cancellationToken = default)
    {
        if (mode == GameMode.Daily)
        {
            dayNumber = await GameService.GetTodayDayNumberAsync(cancellationToken);
        }

        await GameStateStore.Init(mode switch
        {
            GameMode.Daily => $"daily-{dayNumber}",
            GameMode.Archive => $"archive-{dayNumber}",
            GameMode.Practice => "practice-current",
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        });

        var playerId = await GetPlayerIdAsync();

        var gameState = await GameStateStore.GetAsync();

        if (gameState is not null)
        {
            GameState = gameState;

            if (GameState.PlayerId != playerId)
            {
                GameState.PlayerId = playerId;
                await GameStateStore.SaveAsync(GameState);
            }

            return LoadGameResult.LoadedExisting;
        }

        if (mode == GameMode.Daily)
        {
            GameState = await GameService.StartDailyAsync(cancellationToken);
        }
        else if (mode == GameMode.Archive)
        {
            if (!dayNumber.HasValue)
            {
                return LoadGameResult.Failed;
            }

            GameState = await GameService.StartArchivalAsync(dayNumber.Value, cancellationToken);
        }
        else
        {
            GameState = await GameService.StartPracticeAsync(cancellationToken);
        }

        GameState.PlayerId = playerId;
        await GameStateStore.SaveAsync(GameState);

        return LoadGameResult.CreatedNew;
    }

    public async Task<LoadGameResult> LoadOrCreateAsync(GameMode mode, int? dayNumber = null, CancellationToken cancellationToken = default)
    {
        return await StartGameAsync(mode, dayNumber, cancellationToken);
    }

    public async Task<GuessResult?> MakeGuessAsync(int characterId, CancellationToken cancellationToken = default)
    {
        if (GameState is null)
        {
            throw new InvalidOperationException("Game state is not initialized.");
        }

        if (GameState.IsFinished)
            return null;

        if (GameState.GuessesMade.Any(g => g.Character.Id == characterId))
            return null;

        var wasFirstGuess = GameState.GuessesMade.Count == 0;
        var result = await CharacterComparer.CompareAsync(GameState.Game.TargetId, characterId, cancellationToken);

        if (result is null)
            return null;

        // record the guess
        GameState.GuessesMade.Add(result);

        // Check win condition
        if (result.IsCorrect)
        {
            GameState.IsWon = true;
        }

        // Check loss condition
        else if (GameState.GuessesMade.Count >= MaxGuesses)
        {
            GameState.IsLost = true;
        }

        await GameStateStore.SaveAsync(GameState);

        if (wasFirstGuess)
        {
            await StatisticsService.RecordGameStartedAsync(new GameStartedEvent
            {
                Timestamp = DateTime.UtcNow,
                DayNumber = GameState.Game.DayNumber,
                PlayerId = GameState.PlayerId,
                GameId = GameState.GameId,
                GameMode = GameState.GameMode,
                TargetCharacterId = GameState.Game.TargetId,
            });
        }

        await StatisticsService.RecordGuessMadeAsync(new GuessMadeEvent
        {
            Timestamp = DateTime.UtcNow,
            PlayerId = GameState.PlayerId,
            GameId = GameState.GameId,
            GuessedCharacterId = characterId,
        });

        if (GameState.IsFinished && !GameState.StatsRecorded)
        {
            await StatisticsService.RecordGameFinishedAsync(new GameFinishedEvent
            {
                Timestamp = DateTime.UtcNow,
                GameMode = GameState.GameMode,
                DayNumber = GameState.Game.DayNumber,
                PlayerId = GameState.PlayerId,
                GameId = GameState.GameId,
                GuessCount = GameState.GuessesMade.Count,
                IsWon = GameState.IsWon,
            });

            await MarkStatsRecordedAsync(cancellationToken);
        }

        return result;
    }

    public async Task<string> GetTargetName(CancellationToken cancellationToken = default)
    {
        var character = await CharacterStore.GetCharacterAsync(GameState.Game.TargetId, cancellationToken);
        return character?.Name ?? string.Empty;
    }

    public async Task MarkPopupAsSeenAsync(CancellationToken cancellationToken = default)
    {
        GameState.SeenPopup = true;
        await GameStateStore.SaveAsync(GameState);
    }

    public async Task MarkStatsRecordedAsync(CancellationToken cancellationToken = default)
    {
        GameState.StatsRecorded = true;
        await GameStateStore.SaveAsync(GameState);
    }

    private async Task<Guid> GetPlayerIdAsync()
    {
        var playerData = await PlayerStatsStore.LoadAsync();

        if (playerData.Id == Guid.Empty)
        {
            throw new InvalidOperationException("Player identity was not initialized.");
        }

        return playerData.Id;
    }
}
