using System.Diagnostics;
using Microsoft.Extensions.Options;
using QSMPDLE.Web.Extensions;
using QSMPDLE.Web.Features.Gameplay.Helpers;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Infrastructure.LocalStorage;
using QSMPDLE.Web.Infrastructure.Persistence;

namespace QSMPDLE.Web.Features.Gameplay.Services;

/// <summary>Owns the browser state while PostgreSQL is the source of truth after a first guess.</summary>
public sealed class GameStateManager(
    IGameStateStore gameStateStore,
    IGameService gameService,
    IDayService dayService,
    IPlayerStatsStore playerStatsStore,
    ICharacterStore characterStore,
    ICharacterComparer characterComparer,
    IGameStatsStore gameStatsStore,
    ILogger<GameStateManager>? logger = null,
    IGameplayAvailabilityService? availability = null,
    TimeProvider? timeProvider = null) : IGameStateManager
{
    // Without configured release dates every clue is live, which is what callers that predate releases expect.
    private readonly IGameplayAvailabilityService _availability =
        availability ?? new GameplayAvailabilityService(dayService, Options.Create(new GameplayOptions()));

    // The state and the store's key are shared by the whole circuit, so operations that use them never overlap.
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public DateOnly EffectiveDate => _availability.GetEffectiveDate(GameState?.Game.PuzzleId);

    public IReadOnlySet<ClueGroup> ActiveClues => _availability.GetActiveClues(EffectiveDate);

    public GameState GameState { get; private set; } = null!;

    public bool IsFirstGame { get; private set; }

    public async Task<LoadGameResult> StartGameAsync(EntryPoint entryPoint, int? puzzleId = null,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var result = await LoadGameAsync(entryPoint, puzzleId, cancellationToken);
            if (result != LoadGameResult.Failed)
            {
                await ResolveFirstGameAsync();
            }

            return result;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task StartNewPracticeGameAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            await gameStateStore.Init("practice-current");
            var playerId = await GetPlayerIdAsync();
            GameState = await gameService.StartPracticeAsync(cancellationToken);
            GameState.PlayerId = playerId;
            GameState.SessionCategory = SessionCategory.Practice;
            GameState.EntryPoint = EntryPoint.Practice;
            GameState.FirstEntryPoint = EntryPoint.Practice;
            await gameStateStore.SaveAsync(GameState);
            await ResolveFirstGameAsync();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<GuessResult?> MakeGuessAsync(int characterId, CancellationToken cancellationToken = default)
    {
        if (GameState is null)
        {
            throw new InvalidOperationException("Game state is not initialized.");
        }

        var gameId = GameState.GameId;
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            // The guess was meant for the game on screen, not for one that loaded while it waited.
            return GameState.GameId == gameId ? await PlayGuessAsync(characterId, cancellationToken) : null;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<string> GetTargetName(CancellationToken cancellationToken = default) =>
        (await characterStore.GetCharacterAsync(GameState.Game.TargetId, cancellationToken))?.Name ?? string.Empty;

    public async Task MarkPopupAsSeenAsync(CancellationToken cancellationToken = default)
    {
        var gameId = GameState.GameId;
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            if (GameState.GameId != gameId)
            {
                return;
            }

            GameState.SeenPopup = true;
            await gameStateStore.SaveAsync(GameState);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task<LoadGameResult> LoadGameAsync(EntryPoint entryPoint, int? puzzleId,
        CancellationToken cancellationToken)
    {
        var category = entryPoint == EntryPoint.Practice ? SessionCategory.Practice : SessionCategory.CanonicalPuzzle;

        if (category == SessionCategory.CanonicalPuzzle)
        {
            puzzleId ??= entryPoint == EntryPoint.Daily ? dayService.GetTodayDayNumber() : null;
            if (!puzzleId.HasValue)
            {
                return LoadGameResult.Failed;
            }

            await gameStateStore.Init($"canonical-{puzzleId.Value}");
        }
        else
        {
            await gameStateStore.Init("practice-current");
        }

        var playerId = await GetPlayerIdAsync();

        if (category == SessionCategory.CanonicalPuzzle)
        {
            var persisted = await gameStatsStore.GetActiveCanonicalSessionAsync(playerId, puzzleId!.Value);
            if (persisted is not null)
            {
                // The server state is authoritative once a first guess was recorded.
                GameState = await CreateStateFromSessionAsync(persisted, entryPoint, cancellationToken);
                await gameStateStore.SaveAsync(GameState);
                return LoadGameResult.LoadedExisting;
            }
        }

        var local = await gameStateStore.GetAsync();
        if (local is not null && await IsCompatibleAsync(local, category, puzzleId, cancellationToken))
        {
            GameState = NormalizeLocalState(local, playerId, entryPoint, category, puzzleId);
            // Browser state can predate a clue, which would deserialize as Wrong, so results are recomputed from character data.
            try
            {
                GameState.GuessesMade = await CompareGuessesAsync(GameState.Game.TargetId,
                    GameState.GuessesMade.Select(guess => guess.Character.Id).ToList(), ActiveClues, cancellationToken);
            }
            catch (ArgumentException)
            {
                // A character removed since the guess keeps its stored results rather than blocking the game from loading.
            }

            await gameStateStore.SaveAsync(GameState);
            return LoadGameResult.LoadedExisting;
        }

        GameState = category == SessionCategory.Practice
            ? await gameService.StartPracticeAsync(cancellationToken)
            : entryPoint == EntryPoint.Daily
                ? await gameService.StartDailyAsync(cancellationToken)
                : await gameService.StartArchivalAsync(puzzleId!.Value, cancellationToken);

        GameState.PlayerId = playerId;
        GameState.SessionCategory = category;
        GameState.EntryPoint = entryPoint;
        GameState.FirstEntryPoint = entryPoint;
        await gameStateStore.SaveAsync(GameState);
        return LoadGameResult.CreatedNew;
    }

    private async Task ResolveFirstGameAsync()
    {
        var firstGameId = await gameStateStore.GetFirstGameIdAsync();
        if (firstGameId is not null)
        {
            IsFirstGame = firstGameId == GameState.GameId;
            return;
        }

        // Browsers that played before the flag existed are recognised by their saved guesses, once.
        if (GameState.GuessesMade.Count > 0 || await gameStateStore.HasGuessedInOtherGameAsync())
        {
            await gameStateStore.SaveFirstGameIdAsync(Guid.Empty);
            IsFirstGame = false;
            return;
        }

        IsFirstGame = true;
    }

    private async Task<GuessResult?> PlayGuessAsync(int characterId, CancellationToken cancellationToken)
    {
        if (GameState.IsFinished || GameState.GuessesMade.Any(guess => guess.Character.Id == characterId))
        {
            return null;
        }

        // The selector only offers released characters and the answer, so this catches calls that bypass it.
        var guessed = await characterStore.GetCharacterAsync(characterId, cancellationToken);
        if (guessed is null || (characterId != GameState.Game.TargetId && !guessed.IsAvailableOn(EffectiveDate)))
        {
            logger?.LogWarning("Rejected a guess of a character that is not available for this game.");
            return null;
        }

        EnsureValidPlayerId();

        var startedAt = Stopwatch.GetTimestamp();
        var stage = "compare";
        var guessRecorded = false;
        var localSaved = false;
        try
        {
            var isFirstGuess = GameState.GuessesMade.Count == 0;
            var result = await characterComparer.CompareAsync(GameState.Game.TargetId, characterId, ActiveClues,
                cancellationToken);

            stage = "record-guess";
            int[] board = [.. GameState.GuessesMade.Select(guess => guess.Character.Id), characterId];
            var stored = await RecordGuessAsync(board);
            guessRecorded = stored is not null;
            if (stored is not null &&
                (stored.GameId != GameState.GameId || !stored.GuessedCharacterIds.SequenceEqual(board)))
            {
                GameState = await CreateStateFromSessionAsync(stored, GameState.EntryPoint, cancellationToken);
                stage = "save-reconciled-local";
                await gameStateStore.SaveAsync(GameState);
                localSaved = true;
                return null;
            }

            GameState.GuessesMade.Add(result);

            GameState.Outcome = GameRules.GetOutcome(GameState.Game.TargetId, board);

            stage = "save-local";
            await gameStateStore.SaveAsync(GameState);
            localSaved = true;

            // Recorded after reconciliation, so the id is the one later loads of this game will carry.
            if (isFirstGuess && IsFirstGame)
            {
                stage = "save-first-game";
                await gameStateStore.SaveFirstGameIdAsync(GameState.GameId);
            }

            return result;
        }
        catch (Exception exception)
        {
            logger?.LogError(exception,
                "Guess failed Stage={Stage} GuessRecorded={GuessRecorded} LocalSaved={LocalSaved} ElapsedMs={ElapsedMs}",
                stage, guessRecorded, localSaved, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            throw;
        }
    }

    /// <summary>
    ///     Null when a practice guess could not be recorded: only statistics depend on it, and the next guess
    ///     sends the whole board again. Canonical games fail instead, because the database restores them.
    /// </summary>
    private async Task<GameSession?> RecordGuessAsync(IReadOnlyList<int> board)
    {
        var now = _timeProvider.GetUtcNow();
        try
        {
            return await gameStatsStore.RecordGuessAsync(CreateSession(now), board, now);
        }
        catch (Exception exception) when (GameState.SessionCategory == SessionCategory.Practice)
        {
            logger?.LogError(exception, "Practice guess was played but not recorded");
            return null;
        }
    }

    // The browser's copy can be edited or outdated, so only the server decides a puzzle's answer.
    private async Task<bool> IsCompatibleAsync(GameState state, SessionCategory category, int? puzzleId,
        CancellationToken cancellationToken) =>
        category == SessionCategory.Practice
            ? state.Game.PuzzleId is null
            : state.Game.PuzzleId == puzzleId
              && state.Game.TargetId ==
              (await characterStore.GetCharacterForDayAsync(puzzleId!.Value, cancellationToken)).Id;

    private static GameState NormalizeLocalState(GameState state, Guid playerId, EntryPoint entryPoint,
        SessionCategory category, int? puzzleId)
    {
        // Game ids are unique across players: one saved under another identity could never be recorded again.
        if (state.PlayerId != playerId)
        {
            state.GameId = Guid.NewGuid();
        }

        state.PlayerId = playerId;
        state.SessionCategory = category;
        state.EntryPoint = entryPoint;
        state.FirstEntryPoint = state.FirstEntryPoint is EntryPoint.Archive or EntryPoint.Daily or EntryPoint.Practice
            ? state.FirstEntryPoint
            : entryPoint;
        state.Game.PuzzleId = puzzleId;
        return state;
    }

    private async Task<GameState> CreateStateFromSessionAsync(GameSession session, EntryPoint entryPoint,
        CancellationToken cancellationToken)
    {
        var characters = await characterStore.GetCharacterMapAsync(cancellationToken);
        var target = characters.GetValueOrDefault(session.TargetCharacterId);
        var activeClues = _availability.GetActiveClues(_availability.GetEffectiveDate(session.PuzzleId));
        // Without its answer, or with a guess that is no longer in the catalog, the rest of the game still opens.
        var comparable = target is null ? [] : session.GuessedCharacterIds.Where(characters.ContainsKey).ToList();
        var guesses = await CompareGuessesAsync(session.TargetCharacterId, comparable, activeClues, cancellationToken);

        return new GameState
        {
            GameId = session.GameId,
            PlayerId = session.PlayerId,
            SessionCategory = session.SessionCategory,
            EntryPoint = entryPoint,
            FirstEntryPoint = session.FirstEntryPoint,
            Game = new Game
            {
                PuzzleId = session.PuzzleId,
                TargetId = session.TargetCharacterId,
                PortraitUrl =
                    target is null ? string.Empty : CharacterUrlHelper.GetPortraitFolder(target.PortraitId)
            },
            Outcome = session.Outcome,
            SeenPopup = session.FinishedOnUtc.HasValue,
            GuessesMade = guesses
        };
    }

    private async Task<List<GuessResult>> CompareGuessesAsync(int targetId, IReadOnlyList<int> guessedIds,
        IReadOnlySet<ClueGroup> activeClues, CancellationToken cancellationToken)
    {
        var guesses = new List<GuessResult>();
        foreach (var guessedId in guessedIds)
        {
            guesses.Add(await characterComparer.CompareAsync(targetId, guessedId, activeClues, cancellationToken));
        }

        return guesses;
    }

    private GameSession CreateSession(DateTimeOffset startedOnUtc) => new()
    {
        GameId = GameState.GameId,
        PlayerId = GameState.PlayerId,
        PuzzleId = GameState.Game.PuzzleId,
        SessionCategory = GameState.SessionCategory,
        FirstEntryPoint = GameState.FirstEntryPoint,
        TargetCharacterId = GameState.Game.TargetId,
        Ruleset = _availability.GetRuleset(EffectiveDate),
        StartedOnUtc = startedOnUtc
    };

    private void EnsureValidPlayerId()
    {
        if (GameState.PlayerId == Guid.Empty)
        {
            throw new InvalidOperationException("Cannot record telemetry without a valid player id.");
        }
    }

    private async Task<Guid> GetPlayerIdAsync()
    {
        var playerId = await playerStatsStore.GetPlayerIdAsync();
        return playerId == Guid.Empty
            ? throw new InvalidOperationException("Player identity was not initialized.")
            : playerId;
    }
}
