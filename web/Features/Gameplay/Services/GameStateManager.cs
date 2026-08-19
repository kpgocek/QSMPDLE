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

        // If this is an archive request and a completed daily exists on the server,
        // prefer the server-side completed session replay and overwrite any local cached state.
        if (mode == GameMode.Archive && dayNumber.HasValue)
        {
            var completedDaily = await StatisticsService.GetPlayerCompletedDailyGameAsync(playerId, dayNumber.Value);

            if (completedDaily is not null)
            {
                GameState = await CreateArchiveReplayStateAsync(completedDaily, playerId, cancellationToken);
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

    private async Task<GameState> CreateArchiveReplayStateAsync(GameSession completedDaily, Guid playerId, CancellationToken cancellationToken = default)
    {
        var dayNumber = completedDaily.DailyNumber
            ?? throw new InvalidOperationException("Completed daily session is missing a day number.");

        var state = new GameState
        {
            // Preserve the original completed session id so any telemetry or lookups
            // that reference the game by id continue to work with the replay.
            GameId = completedDaily.GameId,
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
            GuessesMade = new List<GuessResult>()
        };

        // Ensure portrait is available on replay so the reveal UI can render the
        // correct character image. Fall back to empty if character lookup fails.
        try
        {
            var targetCharacter = await CharacterStore.GetCharacterAsync(completedDaily.TargetCharacterId, cancellationToken);
            if (targetCharacter is not null)
            {
                state.Game.PortraitUrl = targetCharacter.IconUrl;
            }
        }
        catch
        {
            // ignore failures and leave PortraitUrl empty
        }

        var orderedGuesses = completedDaily.Guesses.OrderBy(guess => guess.GuessOrder).ToList();

        for (var i = 0; i < orderedGuesses.Count; i++)
        {
            var guess = orderedGuesses[i];

            // Use the CharacterComparer to produce the same GuessResult as live play.
            var comparison = await CharacterComparer.CompareAsync(completedDaily.TargetCharacterId, guess.GuessedCharacterId, cancellationToken);

            if (comparison is null)
            {
                // Fallback: construct a minimal GuessResult with conservative values
                comparison = new GuessResult
                {
                    Character = new CharacterLookup(guess.GuessedCharacterId, guess.GuessedCharacterId.ToString(), guess.GuessedCharacterId.ToString(), new List<string>(), string.Empty),
                    IsCorrect = guess.GuessedCharacterId == completedDaily.TargetCharacterId,
                    Pronouns = ComparisonResult.Wrong,
                    Languages = ComparisonResult.Wrong,
                    Joined = ComparisonResult.Wrong,
                    Affiliation = ComparisonResult.Wrong,
                    Species = ComparisonResult.Wrong,
                };
            }

            // Ensure first/last flags reflect the archived ordering
            var replay = new GuessResult
            {
                Character = comparison.Character,
                IsCorrect = comparison.IsCorrect,
                IsFirstGuess = i == 0,
                IsLastAllowedGuess = i == orderedGuesses.Count - 1,
                Joined = comparison.Joined,
                Languages = comparison.Languages,
                Pronouns = comparison.Pronouns,
                Affiliation = comparison.Affiliation,
                Species = comparison.Species,
            };

            state.GuessesMade.Add(replay);
        }

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
