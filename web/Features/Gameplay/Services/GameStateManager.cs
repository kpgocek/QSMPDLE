namespace QSMPDLE.Web.Features.Gameplay.Services;

using QSMPDLE.Web.Extensions;
using QSMPDLE.Web.Features.Communication.GameEvents;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Features.Statistics.Services;
using QSMPDLE.Web.Infrastructure.LocalStorage;
using QSMPDLE.Web.Infrastructure.Persistence;

/// <summary>Owns the browser state while PostgreSQL is the source of truth after a first guess.</summary>
public sealed class GameStateManager(
    IGameStateStore gameStateStore,
    IGameService gameService,
    IDayService dayService,
    IPlayerStatsStore playerStatsStore,
    ICharacterStore characterStore,
    ICharacterComparer characterComparer,
    IStatisticsService statisticsService,
    IGameEventBus? eventBus = null) : IGameStateManager
{
    private const int MaxGuesses = 6;

    public GameState GameState { get; private set; } = null!;

    public async Task<LoadGameResult> StartGameAsync(GameMode mode, int? puzzleId = null, CancellationToken cancellationToken = default)
    {
        var entryPoint = ToEntryPoint(mode);
        var category = entryPoint == EntryPoint.Practice ? SessionCategory.Practice : SessionCategory.CanonicalPuzzle;

        if (category == SessionCategory.CanonicalPuzzle)
        {
            puzzleId ??= entryPoint == EntryPoint.Daily ? dayService.GetTodayDayNumber() : null;
            if (!puzzleId.HasValue)
                return LoadGameResult.Failed;

            await gameStateStore.Init($"canonical-{puzzleId.Value}");
        }
        else
        {
            await gameStateStore.Init("practice-current");
        }

        var playerId = await GetPlayerIdAsync();

        if (category == SessionCategory.CanonicalPuzzle)
        {
            var persisted = await statisticsService.GetActiveCanonicalSessionAsync(playerId, puzzleId!.Value);
            if (persisted is not null)
            {
                // The server state is authoritative once a first guess was recorded, but
                // still retire any browser-only legacy copies during this compatibility release.
                var legacyCopies = await LoadLegacyStateAsync(puzzleId.Value, cancellationToken);
                GameState = await CreateStateFromSessionAsync(persisted, entryPoint, cancellationToken);
                await gameStateStore.SaveAsync(GameState);
                if (legacyCopies is not null)
                {
                    await RemoveMigratedLegacyKeysAsync(legacyCopies, puzzleId.Value);
                }
                return LoadGameResult.LoadedExisting;
            }
        }

        var local = await gameStateStore.GetAsync();
        var legacy = local is null && category == SessionCategory.CanonicalPuzzle
            ? await LoadLegacyStateAsync(puzzleId!.Value, cancellationToken)
            : null;
        local ??= legacy?.State;
        if (local is not null && IsCompatible(local, category, puzzleId))
        {
            GameState = NormalizeLocalState(local, playerId, entryPoint, category, puzzleId);
            await gameStateStore.SaveAsync(GameState);
            if (legacy is not null)
            {
                await RemoveMigratedLegacyKeysAsync(legacy, puzzleId!.Value);
            }
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

    public Task<LoadGameResult> LoadOrCreateAsync(GameMode mode, int? puzzleId = null, CancellationToken cancellationToken = default) =>
        StartGameAsync(mode, puzzleId, cancellationToken);

    public async Task StartNewPracticeGameAsync(CancellationToken cancellationToken = default)
    {
        await gameStateStore.Init("practice-current");
        GameState = await gameService.StartPracticeAsync(cancellationToken);
        GameState.PlayerId = await GetPlayerIdAsync();
        GameState.SessionCategory = SessionCategory.Practice;
        GameState.EntryPoint = EntryPoint.Practice;
        GameState.FirstEntryPoint = EntryPoint.Practice;
        await gameStateStore.SaveAsync(GameState);
    }

    public Task StartNewArchivedGameAsync(int dayNumber, CancellationToken cancellationToken = default) =>
        StartGameAsync(GameMode.Archive, dayNumber, cancellationToken);

    public async Task<GuessResult?> MakeGuessAsync(int characterId, CancellationToken cancellationToken = default)
    {
        if (GameState is null)
            throw new InvalidOperationException("Game state is not initialized.");
        if (GameState.IsFinished || GameState.GuessesMade.Any(guess => guess.Character.Id == characterId))
            return null;

        var isFirstGuess = GameState.GuessesMade.Count == 0;
        if (isFirstGuess && GameState.SessionCategory == SessionCategory.CanonicalPuzzle)
        {
            var claimed = await statisticsService.ClaimCanonicalSessionAsync(CreateSessionForStart());
            if (claimed.GameId != GameState.GameId)
            {
                GameState = await CreateStateFromSessionAsync(claimed, GameState.EntryPoint, cancellationToken);
                await gameStateStore.SaveAsync(GameState);
                return null;
            }
        }

        var result = await characterComparer.CompareAsync(GameState.Game.TargetId, characterId, cancellationToken);
        GameState.GuessesMade.Add(new GuessResult
        {
            Character = result.Character,
            IsCorrect = result.IsCorrect,
            IsFirstGuess = isFirstGuess,
            IsLastAllowedGuess = !result.IsCorrect && GameState.GuessesMade.Count + 1 == MaxGuesses,
            Pronouns = result.Pronouns,
            Languages = result.Languages,
            Joined = result.Joined,
            Affiliation = result.Affiliation,
            Species = result.Species,
        });

        if (result.IsCorrect)
            GameState.IsWon = true;
        else if (GameState.GuessesMade.Count >= MaxGuesses)
            GameState.IsLost = true;

        await gameStateStore.SaveAsync(GameState);

        if (isFirstGuess)
        {
            var started = CreateStartedEvent();
            await statisticsService.RecordGameStartedAsync(started);
            if (eventBus is not null)
                await eventBus.PublishAsync(started);
        }

        var guessEvent = new GuessMadeEvent
        {
            Timestamp = DateTime.UtcNow,
            PlayerId = GameState.PlayerId,
            GameId = GameState.GameId,
            GuessedCharacterId = characterId,
            DayNumber = GameState.Game.PuzzleId
        };
        await statisticsService.RecordGuessMadeAsync(guessEvent);
        if (eventBus is not null)
            await eventBus.PublishAsync(guessEvent);

        if (GameState.IsFinished && !GameState.StatsRecorded)
        {
            var finished = new GameFinishedEvent
            {
                Timestamp = DateTime.UtcNow,
                PlayerId = GameState.PlayerId,
                GameId = GameState.GameId,
                GameMode = ToLegacyMode(GameState.EntryPoint),
                SessionCategory = GameState.SessionCategory,
                EntryPoint = GameState.FirstEntryPoint,
                PuzzleId = GameState.Game.PuzzleId,
                DayNumber = GameState.Game.PuzzleId,
                GuessCount = GameState.GuessesMade.Count,
                IsWon = GameState.IsWon,
            };
            await statisticsService.RecordGameFinishedAsync(finished);
            if (eventBus is not null)
                await eventBus.PublishAsync(finished);
            await MarkStatsRecordedAsync(cancellationToken);
        }

        return GameState.GuessesMade[^1];
    }

    public async Task<string> GetTargetName(CancellationToken cancellationToken = default) =>
        (await characterStore.GetCharacterAsync(GameState.Game.TargetId, cancellationToken))?.Name ?? string.Empty;

    public async Task MarkPopupAsSeenAsync(CancellationToken cancellationToken = default)
    {
        GameState.SeenPopup = true;
        await gameStateStore.SaveAsync(GameState);
    }

    public async Task MarkStatsRecordedAsync(CancellationToken cancellationToken = default)
    {
        GameState.StatsRecorded = true;
        await gameStateStore.SaveAsync(GameState);
    }

    private async Task<LegacyGameState?> LoadLegacyStateAsync(int puzzleId, CancellationToken cancellationToken)
    {
        var daily = await gameStateStore.GetByKeyAsync($"daily-{puzzleId}");
        var archive = await gameStateStore.GetByKeyAsync($"archive-{puzzleId}");
        var candidates = new[] { daily, archive }
            .Where(state => state is not null && IsCompatible(state, SessionCategory.CanonicalPuzzle, puzzleId))
            .Cast<GameState>()
            .OrderByDescending(GetMigrationPriority)
            .ThenByDescending(state => state.GuessesMade.Count)
            .ToList();

        return candidates.Count == 0
            ? null
            : new LegacyGameState(candidates[0], daily is not null && IsCompatible(daily, SessionCategory.CanonicalPuzzle, puzzleId), archive is not null && IsCompatible(archive, SessionCategory.CanonicalPuzzle, puzzleId));
    }

    private async Task RemoveMigratedLegacyKeysAsync(LegacyGameState legacy, int puzzleId)
    {
        // The canonical write has already succeeded when this method is called.
        if (legacy.HasDailyState)
            await gameStateStore.RemoveByKeyAsync($"daily-{puzzleId}");
        if (legacy.HasArchiveState)
            await gameStateStore.RemoveByKeyAsync($"archive-{puzzleId}");
    }

    private static int GetMigrationPriority(GameState state) => state switch
    {
        { IsWon: true } => 3,
        { IsLost: true } => 2,
        { GuessesMade.Count: > 0 } => 1,
        _ => 0
    };

    private static bool IsCompatible(GameState state, SessionCategory category, int? puzzleId) =>
        category == SessionCategory.Practice
            ? state.Game.PuzzleId is null
            : state.Game.PuzzleId == puzzleId;

    private static GameState NormalizeLocalState(GameState state, Guid playerId, EntryPoint entryPoint, SessionCategory category, int? puzzleId)
    {
        state.PlayerId = playerId;
        state.SessionCategory = category;
        state.EntryPoint = entryPoint;
        state.FirstEntryPoint = state.FirstEntryPoint is EntryPoint.Archive or EntryPoint.Daily or EntryPoint.Practice
            ? state.FirstEntryPoint
            : entryPoint;
        state.Game.PuzzleId = puzzleId;
        state.GameMode = ToLegacyMode(entryPoint);
        return state;
    }

    private async Task<GameState> CreateStateFromSessionAsync(GameSession session, EntryPoint entryPoint, CancellationToken cancellationToken)
    {
        var target = await characterStore.GetCharacterAsync(session.TargetCharacterId, cancellationToken);
        var orderedGuesses = session.Guesses.OrderBy(guess => guess.GuessOrder).ToList();
        var guesses = new List<GuessResult>();
        for (var index = 0; index < orderedGuesses.Count; index++)
        {
            var comparison = await characterComparer.CompareAsync(session.TargetCharacterId, orderedGuesses[index].GuessedCharacterId, cancellationToken);
            guesses.Add(new GuessResult
            {
                Character = comparison.Character,
                IsCorrect = comparison.IsCorrect,
                IsFirstGuess = index == 0,
                IsLastAllowedGuess = !comparison.IsCorrect && index == MaxGuesses - 1,
                Pronouns = comparison.Pronouns,
                Languages = comparison.Languages,
                Joined = comparison.Joined,
                Affiliation = comparison.Affiliation,
                Species = comparison.Species,
            });
        }

        return new GameState
        {
            GameId = session.GameId,
            PlayerId = session.PlayerId,
            SessionCategory = session.SessionCategory,
            EntryPoint = entryPoint,
            FirstEntryPoint = session.FirstEntryPoint,
            GameMode = ToLegacyMode(entryPoint),
            Game = new Game { PuzzleId = session.PuzzleId ?? session.DailyNumber, TargetId = session.TargetCharacterId, PortraitUrl = target?.IconUrl ?? string.Empty },
            IsWon = session.IsWon,
            IsLost = session.FinishedOnUtc.HasValue && !session.IsWon,
            SeenPopup = session.FinishedOnUtc.HasValue,
            StatsRecorded = session.FinishedOnUtc.HasValue,
            GuessesMade = guesses,
        };
    }

    private GameSession CreateSessionForStart() => new()
    {
        GameId = GameState.GameId,
        PlayerId = GameState.PlayerId,
        PuzzleId = GameState.Game.PuzzleId,
        SessionCategory = GameState.SessionCategory,
        FirstEntryPoint = GameState.FirstEntryPoint,
        Mode = ToLegacyMode(GameState.EntryPoint),
        DailyNumber = GameState.Game.PuzzleId,
        TargetCharacterId = GameState.Game.TargetId,
        StartedOnUtc = DateTimeOffset.UtcNow,
    };

    private GameStartedEvent CreateStartedEvent() => new()
    {
        Timestamp = DateTime.UtcNow,
        PlayerId = GameState.PlayerId,
        GameId = GameState.GameId,
        SessionCategory = GameState.SessionCategory,
        EntryPoint = GameState.FirstEntryPoint,
        PuzzleId = GameState.Game.PuzzleId,
        GameMode = ToLegacyMode(GameState.EntryPoint),
        DayNumber = GameState.Game.PuzzleId,
        TargetCharacterId = GameState.Game.TargetId,
    };

    private async Task<Guid> GetPlayerIdAsync()
    {
        var player = await playerStatsStore.LoadAsync();
        return player.Id == Guid.Empty ? throw new InvalidOperationException("Player identity was not initialized.") : player.Id;
    }

    private static EntryPoint ToEntryPoint(GameMode mode) => mode switch
    {
        GameMode.Daily => EntryPoint.Daily,
        GameMode.Archive => EntryPoint.Archive,
        GameMode.Practice => EntryPoint.Practice,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    private static GameMode ToLegacyMode(EntryPoint entryPoint) => entryPoint switch
    {
        EntryPoint.Daily => GameMode.Daily,
        EntryPoint.Archive => GameMode.Archive,
        _ => GameMode.Practice
    };

    private sealed record LegacyGameState(GameState State, bool HasDailyState, bool HasArchiveState);
}
