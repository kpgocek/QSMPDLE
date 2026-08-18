namespace QSMPDLE.Web.Features.Gameplay.Services;

using System.Linq;
using Models;
using QSMPDLE.Web.Extensions;
using QSMPDLE.Web.Features.Communication.GameEvents;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Features.Statistics.Services;
using QSMPDLE.Web.Infrastructure.LocalStorage;
using QSMPDLE.Web.Infrastructure.Persistence;

// <summary>
// Manages the game state for different game modes (daily, practice, archival).
// </summary>
public class GameStateManager(IGameStateStore GameStateStore, IGameService GameService, IDayService DayService, IPlayerStatsStore PlayerStatsStore,
    ICharacterStore CharacterStore, ICharacterComparer CharacterComparer, IStatisticsService StatisticsService, QSMPDLE.Web.Features.Communication.GameEvents.IGameEventBus? eventBus = null) : IGameStateManager
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
            dayNumber = DayService.GetTodayDayNumber();
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

        if (mode == GameMode.Archive && dayNumber.HasValue && gameState is null)
        {
            var completedDaily = await StatisticsService.GetPlayerCompletedDailyGameAsync(playerId, dayNumber.Value);

            if (completedDaily is not null)
            {
                GameState = CreateArchiveReplayState(completedDaily, playerId);
                await GameStateStore.SaveAsync(GameState);
                return LoadGameResult.LoadedExisting;
            }
        }

        if (gameState is not null)
        {
            if (mode == GameMode.Archive && dayNumber.HasValue && gameState.Game.DayNumber != dayNumber.Value)
            {
                gameState = null;
            }

            if (gameState is null)
            {
                goto CreateNewGame;
            }

            GameState = gameState;

            if (GameState.PlayerId != playerId)
            {
                GameState.PlayerId = playerId;
                await GameStateStore.SaveAsync(GameState);
            }

            return LoadGameResult.LoadedExisting;
        }

    CreateNewGame:
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

            if (eventBus is not null)
            {
                await eventBus.PublishAsync(new GameStartedEvent
                {
                    Timestamp = DateTime.UtcNow,
                    DayNumber = GameState.Game.DayNumber,
                    PlayerId = GameState.PlayerId,
                    GameId = GameState.GameId,
                    GameMode = GameState.GameMode,
                    TargetCharacterId = GameState.Game.TargetId,
                });
            }
        }

        var guessEvent = new GuessMadeEvent
        {
            Timestamp = DateTime.UtcNow,
            PlayerId = GameState.PlayerId,
            GameId = GameState.GameId,
            GuessedCharacterId = characterId,
            DayNumber = GameState.Game?.DayNumber
        };

        await StatisticsService.RecordGuessMadeAsync(guessEvent);
        // Publish internal event so UI components can react to in-memory/local-storage changes
        if (eventBus is not null)
        {
            await eventBus.PublishAsync(guessEvent);
        }

        if (GameState.IsFinished && !GameState.StatsRecorded)
        {
            if (GameState.Game is null)
                throw new NullReferenceException("Game should not be null here.");

            var finishedEvent = new GameFinishedEvent
            {
                Timestamp = DateTime.UtcNow,
                GameMode = GameState.GameMode,
                DayNumber = GameState.Game.DayNumber,
                PlayerId = GameState.PlayerId,
                GameId = GameState.GameId,
                GuessCount = GameState.GuessesMade.Count,
                IsWon = GameState.IsWon,
            };

            await StatisticsService.RecordGameFinishedAsync(finishedEvent);
            // Publish internal event for UI
            if (eventBus is not null)
            {
                await eventBus.PublishAsync(finishedEvent);
            }

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

    private static GameState CreateArchiveReplayState(GameSession completedDaily, Guid playerId)
    {
        var dayNumber = completedDaily.DailyNumber
            ?? throw new InvalidOperationException("Completed daily session is missing a day number.");

        var state = new GameState
        {
            GameId = Guid.NewGuid(),
            PlayerId = playerId,
            GameMode = GameMode.Archive,
            Game = new Game
            {
                DayNumber = dayNumber,
                TargetId = completedDaily.TargetCharacterId,
                PortraitUrl = string.Empty
            },
            IsWon = completedDaily.IsWon,
            IsLost = completedDaily.FinishedOnUtc.HasValue && !completedDaily.IsWon,
            SeenPopup = completedDaily.FinishedOnUtc.HasValue,
            StatsRecorded = true,
            GuessesMade = completedDaily.Guesses
                .OrderBy(guess => guess.GuessOrder)
                .Select(guess => new GuessResult
                {
                    Character = new CharacterLookup(guess.GuessedCharacterId, guess.GuessedCharacterId.ToString(), guess.GuessedCharacterId.ToString(), [], string.Empty),
                    IsCorrect = guess.GuessedCharacterId == completedDaily.TargetCharacterId,
                    IsFirstGuess = guess.GuessOrder == 0,
                    IsLastAllowedGuess = guess.GuessOrder == completedDaily.Guesses.Count - 1,
                    Joined = ComparisonResult.Correct,
                    Languages = ComparisonResult.Correct,
                    Pronouns = ComparisonResult.Correct,
                    Affiliation = ComparisonResult.Correct,
                    Species = ComparisonResult.Correct,
                })
                .ToList()
        };

        return state;
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
