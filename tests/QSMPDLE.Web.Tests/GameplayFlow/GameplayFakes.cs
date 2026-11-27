using QSMPDLE.Web.Features.Gameplay.Helpers;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Infrastructure.LocalStorage;
using QSMPDLE.Web.Infrastructure.Persistence;

namespace QSMPDLE.Web.Tests.GameplayFlow;

internal sealed class InMemoryGameStateStore : IGameStateStore
{
    public string? Key { get; private set; }
    public GameState? State { get; set; }
    private readonly Dictionary<string, GameState> _statesByKey = [];
    public List<GameState> SavedStates { get; } = [];

    public Task Init(string key)
    {
        Key = key;
        return Task.CompletedTask;
    }


    public Task<GameState?> GetAsync() => Task.FromResult(State is null ? null : Clone(State));
    public Guid? FirstGameId { get; set; }

    public Task<Guid?> GetFirstGameIdAsync() => Task.FromResult(FirstGameId);

    public Task SaveFirstGameIdAsync(Guid gameId)
    {
        FirstGameId = gameId;
        return Task.CompletedTask;
    }

    public Task<bool> HasGuessedInOtherGameAsync() =>
        Task.FromResult(_statesByKey.Any(entry => entry.Key != Key && entry.Value.GuessesMade.Count > 0));

    public void SetStateForKey(string key, GameState state) => _statesByKey[key] = Clone(state);
    public bool HasStateForKey(string key) => _statesByKey.ContainsKey(key);
    public GameState GetStateForKey(string key) => _statesByKey[key];

    public Task SaveAsync(GameState state)
    {
        State = Clone(state);
        if (Key is not null)
            _statesByKey[Key] = Clone(state);
        SavedStates.Add(Clone(state));
        return Task.CompletedTask;
    }

    private static GameState Clone(GameState state) => new()
    {
        GameId = state.GameId,
        PlayerId = state.PlayerId,
        Game = new Game
        {
            PuzzleId = state.Game.PuzzleId,
            TargetId = state.Game.TargetId,
            PortraitUrl = state.Game.PortraitUrl,
        },
        SessionCategory = state.SessionCategory,
        EntryPoint = state.EntryPoint,
        FirstEntryPoint = state.FirstEntryPoint,
        IsWon = state.IsWon,
        IsLost = state.IsLost,
        SeenPopup = state.SeenPopup,
        GuessesMade = state.GuessesMade.ConvertAll(Clone),
    };

    private static GuessResult Clone(GuessResult guess) => new()
    {
        Character = guess.Character,
        IsCorrect = guess.IsCorrect,
        Pronouns = guess.Pronouns,
        Languages = guess.Languages,
        Joined = guess.Joined,
        Affiliation = guess.Affiliation,
        Species = guess.Species,
    };
}

internal sealed class InMemoryPlayerStatsStore : IPlayerStatsStore
{
    public PlayerStats Stats { get; set; } = new();
    public int LoadCount { get; private set; }

    public Task<PlayerStats> LoadAsync()
    {
        LoadCount++;
        return Task.FromResult(Clone(Stats));
    }

    public Task<Guid> GetPlayerIdAsync() => Task.FromResult(Stats.Id);

    private static PlayerStats Clone(PlayerStats stats) => new()
    {
        Version = stats.Version,
        Id = stats.Id,
        GamesPlayed = stats.GamesPlayed,
        GamesWon = stats.GamesWon,
        CurrentStreak = stats.CurrentStreak,
        MaxStreak = stats.MaxStreak,
        GuessDistribution = stats.GuessDistribution.ToArray(),
    };
}

internal sealed class InMemoryGameStatsStore : IGameStatsStore
{
    public Dictionary<Guid, GameSession> Sessions { get; } = [];
    public int PlayerGamesLoadCount { get; private set; }

    /// <summary>Simulates an unreachable database for the next recordings.</summary>
    public int FailuresLeft { get; set; }

    /// <summary>Left incomplete to simulate a slow database.</summary>
    public Task Recording { get; set; } = Task.CompletedTask;
    public Task Loading { get; set; } = Task.CompletedTask;

    // Stands in for the row lock and identity lookup of the database store; the recording rules are the session's own.
    public async Task<GameSession> RecordGuessAsync(GameSession game, IReadOnlyList<int> guessedCharacterIds, DateTimeOffset recordedOnUtc)
    {
        await Recording;

        if (FailuresLeft > 0)
        {
            FailuresLeft--;
            throw new InvalidOperationException("Simulated database failure.");
        }

        var existing = game.SessionCategory == SessionCategory.CanonicalPuzzle
            ? Sessions.Values.FirstOrDefault(session => session.PlayerId == game.PlayerId && session.SessionCategory == SessionCategory.CanonicalPuzzle && !session.IsLegacyDuplicate && session.PuzzleId == game.PuzzleId)
            : Sessions.GetValueOrDefault(game.GameId);
        var session = existing ?? (Sessions[game.GameId] = game);
        if (session.GameId == game.GameId)
            session.RecordGuesses(guessedCharacterIds, recordedOnUtc);
        return Clone(session);
    }

    public async Task<GameSession?> GetActiveCanonicalSessionAsync(Guid playerId, int puzzleId)
    {
        await Loading;

        return Sessions.Values.FirstOrDefault(session =>
            session.PlayerId == playerId && session.SessionCategory == SessionCategory.CanonicalPuzzle && !session.IsLegacyDuplicate && session.PuzzleId == puzzleId);
    }

    public Task<IEnumerable<GameSession>> GetPlayerGames(Guid playerId, SessionCategory? category = null)
    {
        PlayerGamesLoadCount++;
        return Task.FromResult<IEnumerable<GameSession>>(Sessions.Values.Where(session => session.PlayerId == playerId && (category is null || session.SessionCategory == category)).ToList());
    }

    public Task<List<GameSession>> GetPlayerCanonicalGamesByPuzzleRangeAsync(Guid playerId, int startPuzzleId, int endPuzzleId) => Task.FromResult(Sessions.Values
        .Where(session => session.PlayerId == playerId && session.SessionCategory == SessionCategory.CanonicalPuzzle && !session.IsLegacyDuplicate && session.PuzzleId >= startPuzzleId && session.PuzzleId <= endPuzzleId)
        .Select(Clone).ToList());


    private static GameSession Clone(GameSession stats) => new()
    {
        GameId = stats.GameId,
        PlayerId = stats.PlayerId,
        PuzzleId = stats.PuzzleId,
        SessionCategory = stats.SessionCategory,
        FirstEntryPoint = stats.FirstEntryPoint,
        IsLegacyDuplicate = stats.IsLegacyDuplicate,
        TargetCharacterId = stats.TargetCharacterId,
        Ruleset = stats.Ruleset,
        StartedOnUtc = stats.StartedOnUtc,
        FinishedOnUtc = stats.FinishedOnUtc,
        IsWon = stats.IsWon,
        Guesses = stats.Guesses.Select(guess => new GameGuess
        {
            GameId = guess.GameId,
            GuessOrder = guess.GuessOrder,
            GuessedCharacterId = guess.GuessedCharacterId,
            GuessedOnUtc = guess.GuessedOnUtc,
        }).ToList(),
    };
}

internal sealed class InMemoryCharacterStore : ICharacterStore
{
    private readonly Dictionary<int, Character> _characters = [];
    private readonly Dictionary<int, CharacterLookup> _lookups = [];
    private readonly Dictionary<int, int> _dailyTargets = [];
    private readonly int _randomCharacterId;

    public InMemoryCharacterStore(IEnumerable<Character> characters, int randomCharacterId, IDictionary<int, int>? dailyTargets = null)
    {
        foreach (var character in characters)
        {
            _characters[character.Id] = character;
            _lookups[character.Id] = new CharacterLookup(character.Id, character.Name, character.MinecraftUsername, character.Aliases, CharacterUrlHelper.GetPortraitFolder(character.PortraitId));
        }

        _randomCharacterId = randomCharacterId;
        if (dailyTargets is not null)
        {
            foreach (var entry in dailyTargets)
            {
                _dailyTargets[entry.Key] = entry.Value;
            }
        }
    }

    public Task<IReadOnlyList<Character>> GetCharactersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Character>>(_characters.Values.ToList());

    public Task<Character?> GetCharacterAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(_characters.TryGetValue(id, out var character) ? character : null);

    public Task<IReadOnlyList<CharacterLookup>> GetLookupsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CharacterLookup>>(_lookups.Values.OrderBy(character => character.Name).ToList());

    public Task<IReadOnlyList<CharacterLookup>> GetLookupsAvailableOnAsync(DateOnly date, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CharacterLookup>>(_lookups.Values.Where(lookup => _characters[lookup.Id].IsAvailableOn(date)).OrderBy(character => character.Name).ToList());

    public Task<CharacterLookup?> GetLookupAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(_lookups.TryGetValue(id, out var lookup) ? lookup : null);

    public Task<Character> GetCharacterForDayAsync(int dayNumber, CancellationToken cancellationToken = default)
    {
        if (_dailyTargets.TryGetValue(dayNumber, out var characterId) && _characters.TryGetValue(characterId, out var character))
        {
            return Task.FromResult(character);
        }

        return Task.FromResult(_characters[_randomCharacterId]);
    }

    public Task<Character> GetRandomCharacterAsync(DateOnly availableOn, CancellationToken cancellationToken = default)
    {
        var configured = _characters[_randomCharacterId];
        return Task.FromResult(configured.IsAvailableOn(availableOn)
            ? configured
            : _characters.Values.Where(character => character.IsAvailableOn(availableOn)).OrderBy(character => character.Id).First());
    }
}

internal static class TestSessions
{
    /// <summary>Arranges a stored guess directly, whatever state the session is in.</summary>
    public static void AddGuess(this GameSession session, int guessedCharacterId) => session.Guesses.Add(new GameGuess
    {
        GameId = session.GameId,
        GuessOrder = session.Guesses.Count,
        GuessedCharacterId = guessedCharacterId,
    });
}

internal static class TestCharacters
{
    public static Character Create(int id, string name, int joinDay, int languages, string[] pronouns, string[] affiliations, string[] species) => new()
    {
        Id = id,
        Name = name,
        MinecraftUsername = name.ToLowerInvariant(),
        Aliases = [],
        PortraitId = Guid.NewGuid(),
        Pronouns = pronouns.ToList(),
        LanguageCount = languages,
        Affiliations = affiliations.ToList(),
        Species = species.ToList(),
        JoinDayNumber = joinDay,
    };
}

/// <summary>A clock the test moves by hand, for release-date and day-boundary checks.</summary>
internal sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
