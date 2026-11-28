using QSMPDLE.Web.Features.Archive.Models;
using QSMPDLE.Web.Features.Archive.Services;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Features.Statistics.Services;
using QSMPDLE.Web.Infrastructure.LocalStorage;
using QSMPDLE.Web.Infrastructure.Persistence;
using QSMPDLE.Web.Tests.GameplayFlow;

namespace QSMPDLE.Web.Tests.Services;

public sealed class ArchiveStatusServiceTests
{
    [Fact]
    public async Task NoSession_Returns_NotStarted()
    {
        var playerId = Guid.NewGuid();
        var playerStatsStore = new TestPlayerStatsStore(playerId);
        var store = new TestGameStatsStore();
        var dayService = new DayService();

        var svc = new ArchiveStatusService(playerStatsStore, store, dayService, new TestArchiveGameStateSource());

        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2);
        var end = start.AddDays(2);

        var result = await svc.GetStatusesAsync(start, end);

        Assert.Equal(end.DayNumber - start.DayNumber + 1, result.Count);
        Assert.All(result.Values, v => Assert.Equal(DayStatus.NotStarted, v));
    }

    [Fact]
    public async Task GetStatuses_KnownStoredDays_ReadsOnlyThoseDays()
    {
        var dayService = new DayService();
        var start = dayService.GetFirstDay().AddDays(-14);
        var end = dayService.GetFirstDay().AddDays(15);
        var source = new TestArchiveGameStateSource { StoredDays = new HashSet<int> { 3 } };
        var svc = new ArchiveStatusService(new TestPlayerStatsStore(Guid.NewGuid()), new TestGameStatsStore(),
            dayService, source);

        await svc.GetStatusesAsync(start, end);

        Assert.All(source.LoadedDays, day => Assert.Equal(3, day));
        Assert.NotEmpty(source.LoadedDays);
    }

    [Fact]
    public async Task InProgress_When_UnfinishedSession()
    {
        var playerId = Guid.NewGuid();
        var playerStatsStore = new TestPlayerStatsStore(playerId);
        var dayService = new DayService();
        var startedOnUtc = DateTimeOffset.UtcNow;
        var session = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = playerId,
            StartedOnUtc = startedOnUtc,
            PuzzleId = dayService.GetArchiveDayNumber(DateOnly.FromDateTime(startedOnUtc.UtcDateTime))
        };

        var store = new TestGameStatsStore(new[] { session });
        var svc = new ArchiveStatusService(playerStatsStore, store, dayService, new TestArchiveGameStateSource());

        var start = DateOnly.FromDateTime(session.StartedOnUtc.UtcDateTime);
        var result = await svc.GetStatusesAsync(start, start);

        Assert.Single(result);
        Assert.Equal(DayStatus.InProgress, result.Values.First());
    }

    [Fact]
    public async Task Won_When_Completed_Win()
    {
        var playerId = Guid.NewGuid();
        var playerStatsStore = new TestPlayerStatsStore(playerId);
        var dayService = new DayService();
        var startedOnUtc = DateTimeOffset.UtcNow.AddDays(-1);
        var session = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = playerId,
            IsWon = true,
            StartedOnUtc = startedOnUtc,
            FinishedOnUtc = startedOnUtc.AddMinutes(5),
            PuzzleId = dayService.GetArchiveDayNumber(DateOnly.FromDateTime(startedOnUtc.UtcDateTime))
        };

        var store = new TestGameStatsStore(new[] { session });
        var svc = new ArchiveStatusService(playerStatsStore, store, dayService, new TestArchiveGameStateSource());

        var day = DateOnly.FromDateTime(session.StartedOnUtc.UtcDateTime);
        var result = await svc.GetStatusesAsync(day, day);

        Assert.Single(result);
        Assert.Equal(DayStatus.Won, result.Values.First());
    }

    [Fact]
    public async Task Lost_When_Completed_Loss()
    {
        var playerId = Guid.NewGuid();
        var playerStatsStore = new TestPlayerStatsStore(playerId);
        var dayService = new DayService();
        var startedOnUtc = DateTimeOffset.UtcNow.AddDays(-1);
        var session = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = playerId,
            IsWon = false,
            StartedOnUtc = startedOnUtc,
            FinishedOnUtc = startedOnUtc.AddMinutes(5),
            PuzzleId = dayService.GetArchiveDayNumber(DateOnly.FromDateTime(startedOnUtc.UtcDateTime))
        };

        var store = new TestGameStatsStore(new[] { session });
        var svc = new ArchiveStatusService(playerStatsStore, store, dayService, new TestArchiveGameStateSource());

        var day = DateOnly.FromDateTime(session.StartedOnUtc.UtcDateTime);
        var result = await svc.GetStatusesAsync(day, day);

        Assert.Single(result);
        Assert.Equal(DayStatus.Lost, result.Values.First());
    }

    [Fact]
    public async Task GetStatuses_SixWrongGuessesWithoutASavedFinish_IsLost()
    {
        var playerId = Guid.NewGuid();
        var dayService = new DayService();
        var day = dayService.GetFirstDay().AddDays(13);
        var session = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = playerId,
            TargetCharacterId = 100,
            PuzzleId = dayService.GetArchiveDayNumber(day)
        };
        for (var guess = 1; guess <= 6; guess++)
        {
            session.AddGuess(guess);
        }

        var svc = new ArchiveStatusService(new TestPlayerStatsStore(playerId),
            new TestGameStatsStore(new[] { session }), dayService, new TestArchiveGameStateSource());

        var result = await svc.GetStatusesAsync(day, day);

        Assert.Equal(DayStatus.Lost, result.Values.Single());
    }

    [Fact]
    public async Task Ignores_Other_Player_Sessions()
    {
        var playerId = Guid.NewGuid();
        var playerStatsStore = new TestPlayerStatsStore(playerId);
        var other = Guid.NewGuid();

        var dayService = new DayService();
        var startedOnUtc = DateTimeOffset.UtcNow.AddDays(-1);
        var session = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = other,
            IsWon = true,
            StartedOnUtc = startedOnUtc,
            FinishedOnUtc = startedOnUtc.AddMinutes(5),
            PuzzleId = dayService.GetArchiveDayNumber(DateOnly.FromDateTime(startedOnUtc.UtcDateTime))
        };

        var store = new TestGameStatsStore(new[] { session });
        var svc = new ArchiveStatusService(playerStatsStore, store, dayService, new TestArchiveGameStateSource());

        var day = DateOnly.FromDateTime(session.StartedOnUtc.UtcDateTime);
        var result = await svc.GetStatusesAsync(day, day);

        Assert.Single(result);
        Assert.Equal(DayStatus.NotStarted, result.Values.First());
    }

    [Fact]
    public async Task BatchedQuery_Is_Called_Once()
    {
        var playerId = Guid.NewGuid();
        var playerStatsStore = new TestPlayerStatsStore(playerId);
        var dayService = new DayService();
        var startedOnUtc = DateTimeOffset.UtcNow.AddDays(-2);
        var session = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = playerId,
            IsWon = true,
            StartedOnUtc = startedOnUtc,
            FinishedOnUtc = startedOnUtc.AddMinutes(5),
            PuzzleId = dayService.GetArchiveDayNumber(DateOnly.FromDateTime(startedOnUtc.UtcDateTime))
        };

        var store = new TestGameStatsStore(new[] { session });
        var svc = new ArchiveStatusService(playerStatsStore, store, dayService, new TestArchiveGameStateSource());

        var start = DateOnly.FromDateTime(session.StartedOnUtc.UtcDateTime).AddDays(-1);
        var end = start.AddDays(3);

        _ = await svc.GetStatusesAsync(start, end);

        Assert.Equal(1, store.QueryCount);
    }

    [Fact]
    public async Task ArchiveEntryFinish_CountsAsPlayedWithoutTouchingLocalTotalsOrStreak()
    {
        var playerId = Guid.NewGuid();
        var playerStatsStore = new TestPlayerStatsStore(new PlayerStats
        {
            Id = playerId,
            GamesPlayed = 2,
            GamesWon = 1,
            CurrentStreak = 2,
            MaxStreak = 3
        });
        var gameStatsStore = new TestGameStatsStore([FinishedSession(playerId, EntryPoint.Archive, 1, true)]);

        var local = await playerStatsStore.LoadAsync();
        Assert.Equal(2, local.GamesPlayed);
        var stats = PlayerStatsProjection.Create(local, await gameStatsStore.GetPlayerGames(playerId),
            new DayService());
        Assert.Equal(1, stats.GamesPlayed);
        Assert.Equal(0, stats.CurrentStreak);
        Assert.Equal(0, stats.MaxStreak);
    }

    [Fact]
    public async Task LateDailyEntryLoss_CountsAsPlayedWithoutAStreak()
    {
        var playerId = Guid.NewGuid();
        var playerStatsStore = new TestPlayerStatsStore(new PlayerStats { Id = playerId });
        var gameStatsStore = new TestGameStatsStore([FinishedSession(playerId, EntryPoint.Daily, 18, false)]);

        var stats = PlayerStatsProjection.Create(await playerStatsStore.LoadAsync(),
            await gameStatsStore.GetPlayerGames(playerId), new DayService());

        Assert.Equal(1, stats.GamesPlayed);
        Assert.Equal(0, stats.GamesWon);
        Assert.Equal(0, stats.CurrentStreak);
        Assert.Equal(0, stats.MaxStreak);
    }

    [Fact]
    public async Task DateToArchiveDayMapping_UsesFirstDayOrigin()
    {
        var playerId = Guid.NewGuid();
        var playerStatsStore = new TestPlayerStatsStore(playerId);
        var dayService = new DayService();
        const int archiveDay = 4;
        var finishedOnUtc = DateTimeOffset.UtcNow;
        var session = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = playerId,
            IsWon = true,
            PuzzleId = archiveDay,
            FinishedOnUtc = finishedOnUtc
        };

        var store = new TestGameStatsStore(new[] { session });
        var svc = new ArchiveStatusService(playerStatsStore, store, new DayService(), new TestArchiveGameStateSource());

        var day = dayService.GetArchiveDate(archiveDay);
        var result = await svc.GetStatusesAsync(day, day);

        Assert.Single(result);
        Assert.Equal(DayStatus.Won, result.Values.First());
    }

    [Fact]
    public async Task Uses_LocalStorage_When_Server_Has_No_Status()
    {
        var playerId = Guid.NewGuid();
        var playerStatsStore = new TestPlayerStatsStore(playerId);
        var dayService = new DayService();
        const int archiveDay = 5;

        var store = new TestGameStatsStore();
        var localSource = new TestArchiveGameStateSource
        {
            State = new GameState
            {
                GameId = Guid.NewGuid(),
                PlayerId = playerId,
                Game = new Game { PuzzleId = archiveDay, TargetId = 1, PortraitUrl = "test" },
                IsWon = false,
                IsLost = false,
                GuessesMade = [new GuessResult()]
            }
        };

        var svc = new ArchiveStatusService(playerStatsStore, store, dayService, localSource);

        var day = dayService.GetArchiveDate(archiveDay);
        var result = await svc.GetStatusesAsync(day, day);

        Assert.Single(result);
        Assert.Equal(DayStatus.InProgress, result.Values.First());
    }

    [Fact]
    public async Task Prefers_More_Advanced_Status_When_Merging()
    {
        var playerId = Guid.NewGuid();
        var playerStatsStore = new TestPlayerStatsStore(playerId);
        var dayService = new DayService();
        const int archiveDay = 6;
        var startedOnUtc = DateTimeOffset.UtcNow;

        var session = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = playerId,
            PuzzleId = archiveDay,
            IsWon = false,
            StartedOnUtc = startedOnUtc
        };

        var store = new TestGameStatsStore(new[] { session });
        var localSource = new TestArchiveGameStateSource
        {
            State = new GameState
            {
                GameId = session.GameId,
                PlayerId = playerId,
                Game = new Game { PuzzleId = archiveDay, TargetId = 1, PortraitUrl = "test" },
                IsWon = true,
                GuessesMade = [new GuessResult()]
            }
        };

        var svc = new ArchiveStatusService(playerStatsStore, store, dayService, localSource);

        var day = dayService.GetArchiveDate(archiveDay);
        var result = await svc.GetStatusesAsync(day, day);

        Assert.Single(result);
        Assert.Equal(DayStatus.InProgress, result.Values.First());
    }

    private static GameSession FinishedSession(Guid playerId, EntryPoint entryPoint, int puzzleId, bool isWon) => new()
    {
        GameId = Guid.NewGuid(),
        PlayerId = playerId,
        StartedOnUtc = DateTimeOffset.UtcNow,
        FinishedOnUtc = DateTimeOffset.UtcNow.AddMinutes(3),
        PuzzleId = puzzleId,
        TargetCharacterId = 15,
        SessionCategory = SessionCategory.CanonicalPuzzle,
        FirstEntryPoint = entryPoint,
        IsWon = isWon
    };

    private sealed class TestArchiveGameStateSource : IArchiveGameStateSource
    {
        public GameState? State { get; init; }

        public IReadOnlySet<int>? StoredDays { get; init; }
        public List<int> LoadedDays { get; } = [];

        public Task<GameState?> LoadAsync(int dayNumber, CancellationToken cancellationToken = default)
        {
            LoadedDays.Add(dayNumber);
            return Task.FromResult(State);
        }

        public Task<IReadOnlySet<int>?> GetStoredDayNumbersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(StoredDays);
    }

    private sealed class TestGameStatsStore(IEnumerable<GameSession>? sessions = null) : IGameStatsStore
    {
        private readonly List<GameSession> sessions = sessions?.ToList() ?? [];
        public int QueryCount { get; private set; }

        public Task<GameSession> RecordGuessAsync(GameSession game, IReadOnlyList<int> guessedCharacterIds,
            DateTimeOffset recordedOnUtc) =>
            throw new NotSupportedException();

        public Task<IEnumerable<GameSession>> GetPlayerGames(Guid playerId, SessionCategory? category = null) =>
            Task.FromResult<IEnumerable<GameSession>>(sessions.Where(session => session.PlayerId == playerId).ToList());

        public Task<List<GameSession>> GetPlayerCanonicalGamesByPuzzleRangeAsync(Guid playerId, int startPuzzleId,
            int endPuzzleId)
        {
            QueryCount++;

            var list = sessions
                .Where(s => s.PlayerId == playerId && s.PuzzleId >= startPuzzleId && s.PuzzleId <= endPuzzleId)
                .ToList();

            return Task.FromResult(list);
        }
    }

    private sealed class TestPlayerStatsStore(PlayerStats stats) : IPlayerStatsStore
    {
        public TestPlayerStatsStore(Guid playerId) : this(new PlayerStats { Id = playerId }) { }

        public Task<PlayerStats> LoadAsync() => Task.FromResult(stats);
    }
}
