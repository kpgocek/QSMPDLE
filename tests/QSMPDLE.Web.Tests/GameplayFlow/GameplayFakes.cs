using QSMPDLE.Web.Features.Communication.GameEvents;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Features.Statistics.Services;
using QSMPDLE.Web.Infrastructure.LocalStorage;
using QSMPDLE.Web.Infrastructure.Persistence;

namespace QSMPDLE.Web.Tests.GameplayFlow;

internal sealed class InMemoryGameStateStore : IGameStateStore
{
    public string? Key { get; private set; }
    public GameState? State { get; set; }
    public List<GameState> SavedStates { get; } = [];

    public Task Init(string key)
    {
        Key = key;
        return Task.CompletedTask;
    }


    public Task<GameState?> GetAsync() => Task.FromResult(State is null ? null : Clone(State));

    public Task SaveAsync(GameState state)
    {
        State = Clone(state);
        SavedStates.Add(Clone(state));
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        State = null;
        Key = null;
        return Task.CompletedTask;
    }

    private static GameState Clone(GameState state) => new()
    {
        GameId = state.GameId,
        PlayerId = state.PlayerId,
        Game = new Game
        {
            DayNumber = state.Game.DayNumber,
            TargetId = state.Game.TargetId,
            PortraitUrl = state.Game.PortraitUrl,
        },
        GameMode = state.GameMode,
        IsWon = state.IsWon,
        IsLost = state.IsLost,
        SeenPopup = state.SeenPopup,
        StatsRecorded = state.StatsRecorded,
        GuessesMade = state.GuessesMade.ConvertAll(Clone),
    };

    private static GuessResult Clone(GuessResult guess) => new()
    {
        Character = guess.Character,
        IsCorrect = guess.IsCorrect,
        IsFirstGuess = guess.IsFirstGuess,
        IsLastAllowedGuess = guess.IsLastAllowedGuess,
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
    public int SaveCount { get; private set; }

    public Task<PlayerStats> LoadAsync()
    {
        LoadCount++;
        return Task.FromResult(Clone(Stats));
    }

    public Task SaveAsync(PlayerStats stats)
    {
        SaveCount++;
        Stats = Clone(stats);
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        Stats = new PlayerStats();
        return Task.CompletedTask;
    }

    private static PlayerStats Clone(PlayerStats stats) => new()
    {
        Version = stats.Version,
        Id = stats.Id,
        GamesPlayed = stats.GamesPlayed,
        GamesWon = stats.GamesWon,
        CurrentStreak = stats.CurrentStreak,
        MaxStreak = stats.MaxStreak,
        LastCompletedDayNumber = stats.LastCompletedDayNumber,
        LastPlayedDailyGameId = stats.LastPlayedDailyGameId,
        GuessDistribution = stats.GuessDistribution.ToArray(),
    };
}

internal sealed class InMemoryGameStatsStore : IGameStatsStore
{
    public Dictionary<Guid, GameSession> Sessions { get; } = [];
    public List<GameSession> SavedSessions { get; } = [];

    public Task<GameSession> LoadOrNewAsync(Guid gameId)
    {
        if (!Sessions.TryGetValue(gameId, out var session))
        {
            session = new GameSession { GameId = gameId };
            Sessions[gameId] = session;
        }

        return Task.FromResult(session);
    }

    public Task SaveAsync(GameSession stats)
    {
        Sessions[stats.GameId] = Clone(stats);
        SavedSessions.Add(Clone(stats));
        return Task.CompletedTask;
    }

    public Task<IEnumerable<GameSession>> GetPlayerGames(Guid playerId)
        => Task.FromResult<IEnumerable<GameSession>>(Sessions.Values.Where(session => session.PlayerId == playerId).ToList());

    public Task<GameSession?> GetPlayerCompletedDailyGameAsync(Guid playerId, int dailyNumber)
        => Task.FromResult(Sessions.Values.FirstOrDefault(session =>
            session.PlayerId == playerId &&
            session.Mode == GameMode.Daily &&
            session.DailyNumber == dailyNumber &&
            session.FinishedOnUtc.HasValue));

    public Task<List<int>> GetPlayerCompletedDailyNumbersAsync(Guid playerId)
    {
        var numbers = Sessions.Values
            .Where(s => s.PlayerId == playerId && s.Mode == GameMode.Daily && s.FinishedOnUtc.HasValue && s.DailyNumber.HasValue)
            .Select(s => s.DailyNumber!.Value)
            .Distinct()
            .ToList();

        return Task.FromResult(numbers);
    }

    public Task<List<GameSession>> GetPlayerDailyGamesByNumberRangeAsync(Guid playerId, int startNumber, int endNumber)
    {
        var list = Sessions.Values
            .Where(s => s.PlayerId == playerId && s.Mode == GameMode.Daily && s.DailyNumber.HasValue)
            .Where(s => s.DailyNumber!.Value >= startNumber && s.DailyNumber!.Value <= endNumber)
            .Select(Clone)
            .ToList();

        return Task.FromResult(list);
    }

    public Task<GlobalStatsView> GetGlobalStatsAsync() => throw new NotImplementedException();
    public Task<List<DailyActivePlayersData>> GetDailyActivePlayersAsync(DateOnly? from) => throw new NotImplementedException();
    public Task<List<NewVsReturningPlayersData>> GetNewVsReturningPlayersAsync(DateOnly? from) => throw new NotImplementedException();
    public Task<PlayerActivityDistributionData[]> GetPlayerActivityDistributionAsync() => throw new NotImplementedException();
    public Task<GamesPerPlayerStats> GetGamesPerPlayerStatsAsync() => throw new NotImplementedException();
    public Task<RetentionStats> GetRetentionStatsAsync() => throw new NotImplementedException();
    public Task<GlobalCharacterStats> GetGlobalCharacterStatsAsync() => throw new NotImplementedException();
    public Task<PlayerCharacterStats> GetPlayerCharacterStatsAsync(Guid playerId) => throw new NotImplementedException();
    public Task<DateOnly?> GetPlayerFirstGameDateAsync(Guid playerId) => throw new NotImplementedException();

    private static GameSession Clone(GameSession stats) => new()
    {
        GameId = stats.GameId,
        PlayerId = stats.PlayerId,
        Mode = stats.Mode,
        DailyNumber = stats.DailyNumber,
        TargetCharacterId = stats.TargetCharacterId,
        StartedOnUtc = stats.StartedOnUtc,
        FinishedOnUtc = stats.FinishedOnUtc,
        IsWon = stats.IsWon,
        Guesses = stats.Guesses.Select(guess => new GameGuess
        {
            GameId = guess.GameId,
            GuessOrder = guess.GuessOrder,
            GuessedCharacterId = guess.GuessedCharacterId,
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
            _lookups[character.Id] = new CharacterLookup(character.Id, character.Name, character.MinecraftUsername, character.Aliases, character.IconUrl);
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

    public Task<Character> GetRandomCharacterAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_characters[_randomCharacterId]);
}

internal sealed class InMemoryStatisticsService : IStatisticsService
{
    public List<GameStartedEvent> GameStartedEvents { get; } = [];
    public List<GuessMadeEvent> GuessMadeEvents { get; } = [];
    public List<GameFinishedEvent> GameFinishedEvents { get; } = [];
    public Dictionary<Guid, GameSession> Sessions { get; } = [];
    public PlayerStats PlayerStats { get; set; } = new();

    public Task RecordGameStartedAsync(GameStartedEvent eventData)
    {
        GameStartedEvents.Add(eventData);
        var session = LoadOrCreate(eventData.GameId);
        session.PlayerId = eventData.PlayerId;
        session.StartedOnUtc = eventData.Timestamp;
        session.DailyNumber = eventData.DayNumber;
        session.TargetCharacterId = eventData.TargetCharacterId;
        session.Mode = eventData.GameMode;
        return Task.CompletedTask;
    }

    public Task RecordGuessMadeAsync(GuessMadeEvent eventData)
    {
        GuessMadeEvents.Add(eventData);
        LoadOrCreate(eventData.GameId).AddGuess(eventData.GuessedCharacterId);
        return Task.CompletedTask;
    }

    public Task RecordGameFinishedAsync(GameFinishedEvent eventData)
    {
        GameFinishedEvents.Add(eventData);
        var session = LoadOrCreate(eventData.GameId);
        if (session.FinishedOnUtc.HasValue)
        {
            return Task.CompletedTask;
        }

        session.PlayerId = eventData.PlayerId;
        session.FinishedOnUtc = eventData.Timestamp;
        session.IsWon = eventData.IsWon;
        return Task.CompletedTask;
    }

    public Task<PlayerStats> GetPlayerStatsAsync() => Task.FromResult(PlayerStats);
    public Task<GameSession> GetGameStatsAsync(Guid gameId) => Task.FromResult(LoadOrCreate(gameId));
    public Task<GameSession?> GetPlayerCompletedDailyGameAsync(Guid playerId, int dailyNumber)
    {
        return Task.FromResult(Sessions.Values.FirstOrDefault(session =>
            session.PlayerId == playerId &&
            session.Mode == GameMode.Daily &&
            session.DailyNumber == dailyNumber &&
            session.FinishedOnUtc.HasValue));
    }

    public Task<List<int>> GetPlayerCompletedDailyNumbersAsync(Guid playerId)
    {
        var numbers = Sessions.Values
            .Where(s => s.PlayerId == playerId && s.Mode == GameMode.Daily && s.FinishedOnUtc.HasValue && s.DailyNumber.HasValue)
            .Select(s => s.DailyNumber!.Value)
            .Distinct()
            .ToList();

        return Task.FromResult(numbers);
    }

    private GameSession LoadOrCreate(Guid gameId)
    {
        if (!Sessions.TryGetValue(gameId, out var session))
        {
            session = new GameSession { GameId = gameId };
            Sessions[gameId] = session;
        }

        return session;
    }
}
