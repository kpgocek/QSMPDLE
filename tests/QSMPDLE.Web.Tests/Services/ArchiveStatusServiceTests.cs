using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Communication.GameEvents;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Features.Statistics.Services;
using QSMPDLE.Web.Infrastructure.LocalStorage;
using QSMPDLE.Web.Infrastructure.Persistence;
using QSMPDLE.Web.Services;

namespace QSMPDLE.Web.Tests.Services;

public sealed class ArchiveStatusServiceTests
{
    [Fact]
    public async Task NoSession_Returns_NotStarted()
    {
        var playerId = Guid.NewGuid();
        var statsService = new TestStatsService(playerId);
        var store = new TestGameStatsStore();
        var gameService = new TestGameService();

        var svc = new ArchiveStatusService(statsService, store, gameService, new TestArchiveGameStateSource());

        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2);
        var end = start.AddDays(2);

        var result = await svc.GetStatusesAsync(start, end);

        Assert.Equal((end.DayNumber - start.DayNumber) + 1, result.Count);
        Assert.All(result.Values, v => Assert.Equal(DayStatus.NotStarted, v));
    }

    [Fact]
    public async Task InProgress_When_UnfinishedSession()
    {
        var playerId = Guid.NewGuid();
        var statsService = new TestStatsService(playerId);
        var session = new GameSession { GameId = Guid.NewGuid(), PlayerId = playerId, Mode = Features.Gameplay.Models.GameMode.Daily };
        session.StartedOnUtc = DateTimeOffset.UtcNow;
        // assign daily number consistent with TestGameService first day
        var testGameService = new TestGameService();
        session.DailyNumber = DateOnly.FromDateTime(session.StartedOnUtc.UtcDateTime).DayNumber - testGameService.GetFirstDay().DayNumber + 1;

        var store = new TestGameStatsStore(new[] { session });
        var svc = new ArchiveStatusService(statsService, store, testGameService, new TestArchiveGameStateSource());

        var start = DateOnly.FromDateTime(session.StartedOnUtc.UtcDateTime);
        var result = await svc.GetStatusesAsync(start, start);

        Assert.Single(result);
        Assert.Equal(DayStatus.InProgress, result.Values.First());
    }

    [Fact]
    public async Task Won_When_Completed_Win()
    {
        var playerId = Guid.NewGuid();
        var statsService = new TestStatsService(playerId);
        var session = new GameSession { GameId = Guid.NewGuid(), PlayerId = playerId, Mode = Features.Gameplay.Models.GameMode.Daily, IsWon = true };
        session.StartedOnUtc = DateTimeOffset.UtcNow.AddDays(-1);
        session.FinishedOnUtc = session.StartedOnUtc.AddMinutes(5);
        var testGameService = new TestGameService();
        session.DailyNumber = DateOnly.FromDateTime(session.StartedOnUtc.UtcDateTime).DayNumber - testGameService.GetFirstDay().DayNumber + 1;

        var store = new TestGameStatsStore(new[] { session });
        var svc = new ArchiveStatusService(statsService, store, testGameService, new TestArchiveGameStateSource());

        var day = DateOnly.FromDateTime(session.StartedOnUtc.UtcDateTime);
        var result = await svc.GetStatusesAsync(day, day);

        Assert.Single(result);
        Assert.Equal(DayStatus.Won, result.Values.First());
    }

    [Fact]
    public async Task Lost_When_Completed_Loss()
    {
        var playerId = Guid.NewGuid();
        var statsService = new TestStatsService(playerId);
        var session = new GameSession { GameId = Guid.NewGuid(), PlayerId = playerId, Mode = Features.Gameplay.Models.GameMode.Daily, IsWon = false };
        session.StartedOnUtc = DateTimeOffset.UtcNow.AddDays(-1);
        session.FinishedOnUtc = session.StartedOnUtc.AddMinutes(5);
        var testGameService = new TestGameService();
        session.DailyNumber = DateOnly.FromDateTime(session.StartedOnUtc.UtcDateTime).DayNumber - testGameService.GetFirstDay().DayNumber + 1;

        var store = new TestGameStatsStore(new[] { session });
        var svc = new ArchiveStatusService(statsService, store, testGameService, new TestArchiveGameStateSource());

        var day = DateOnly.FromDateTime(session.StartedOnUtc.UtcDateTime);
        var result = await svc.GetStatusesAsync(day, day);

        Assert.Single(result);
        Assert.Equal(DayStatus.Lost, result.Values.First());
    }

    [Fact]
    public async Task Ignores_Other_Player_Sessions()
    {
        var playerId = Guid.NewGuid();
        var statsService = new TestStatsService(playerId);
        var other = Guid.NewGuid();

        var session = new GameSession { GameId = Guid.NewGuid(), PlayerId = other, Mode = Features.Gameplay.Models.GameMode.Daily, IsWon = true };
        session.StartedOnUtc = DateTimeOffset.UtcNow.AddDays(-1);
        session.FinishedOnUtc = session.StartedOnUtc.AddMinutes(5);
        var testGameService = new TestGameService();
        session.DailyNumber = DateOnly.FromDateTime(session.StartedOnUtc.UtcDateTime).DayNumber - testGameService.GetFirstDay().DayNumber + 1;

        var store = new TestGameStatsStore(new[] { session });
        var svc = new ArchiveStatusService(statsService, store, testGameService, new TestArchiveGameStateSource());

        var day = DateOnly.FromDateTime(session.StartedOnUtc.UtcDateTime);
        var result = await svc.GetStatusesAsync(day, day);

        Assert.Single(result);
        Assert.Equal(DayStatus.NotStarted, result.Values.First());
    }

    [Fact]
    public async Task BatchedQuery_Is_Called_Once()
    {
        var playerId = Guid.NewGuid();
        var statsService = new TestStatsService(playerId);
        var session = new GameSession { GameId = Guid.NewGuid(), PlayerId = playerId, Mode = Features.Gameplay.Models.GameMode.Daily, IsWon = true };
        session.StartedOnUtc = DateTimeOffset.UtcNow.AddDays(-2);
        session.FinishedOnUtc = session.StartedOnUtc.AddMinutes(5);
        var testGameService = new TestGameService();
        session.DailyNumber = DateOnly.FromDateTime(session.StartedOnUtc.UtcDateTime).DayNumber - testGameService.GetFirstDay().DayNumber + 1;

        var store = new TestGameStatsStore(new[] { session });
        var svc = new ArchiveStatusService(statsService, store, testGameService, new TestArchiveGameStateSource());

        var start = DateOnly.FromDateTime(session.StartedOnUtc.UtcDateTime).AddDays(-1);
        var end = start.AddDays(3);

        var result = await svc.GetStatusesAsync(start, end);

        Assert.Equal(1, store.QueryCount);
    }

    [Fact]
    public async Task RecordGameFinishedAsync_ArchiveGame_UpdatesArchiveTotalsOnly()
    {
        var playerId = Guid.NewGuid();
        var statsStore = new TestPlayerStatsStore(new PlayerStats { Id = playerId, GamesPlayed = 2, GamesWon = 1, CurrentStreak = 2, MaxStreak = 3, LastCompletedDayNumber = 10 });
        var gameStatsStore = new TestGameStatsStore();
        var service = new StatisticsService(statsStore, gameStatsStore);

        var gameId = Guid.NewGuid();
        await service.RecordGameStartedAsync(new GameStartedEvent
        {
            GameId = gameId,
            PlayerId = playerId,
            GameMode = GameMode.Archive,
            TargetCharacterId = 15,
            DayNumber = 1,
            Timestamp = DateTimeOffset.UtcNow,
        });
        await service.RecordGameFinishedAsync(new GameFinishedEvent
        {
            GameId = gameId,
            PlayerId = playerId,
            GameMode = GameMode.Archive,
            IsWon = true,
            GuessCount = 3,
            DayNumber = 15,
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(3),
        });

        var stats = await statsStore.LoadAsync();

        Assert.Equal(2, stats.GamesPlayed);
        Assert.Equal(1, stats.GamesWon);
        Assert.Equal(2, stats.CurrentStreak);
        Assert.Equal(3, stats.MaxStreak);
        Assert.Equal(10, stats.LastCompletedDayNumber);
        Assert.Equal(1, stats.ArchiveGamesPlayed);
        Assert.Equal(1, stats.ArchiveGamesWon);
        Assert.Equal(0, stats.ArchiveGamesLost);
    }

    [Fact]
    public async Task RecordGameFinishedAsync_DailyGame_LeavesArchiveTotalsUnchanged()
    {
        var playerId = Guid.NewGuid();
        var statsStore = new TestPlayerStatsStore(new PlayerStats { Id = playerId, ArchiveGamesPlayed = 4, ArchiveGamesWon = 2, ArchiveGamesLost = 2 });
        var gameStatsStore = new TestGameStatsStore();
        var service = new StatisticsService(statsStore, gameStatsStore);

        var gameId = Guid.NewGuid();
        await service.RecordGameStartedAsync(new GameStartedEvent
        {
            GameId = gameId,
            PlayerId = playerId,
            GameMode = GameMode.Daily,
            TargetCharacterId = 18,
            DayNumber = 1,
            Timestamp = DateTimeOffset.UtcNow,
        });
        await service.RecordGameFinishedAsync(new GameFinishedEvent
        {
            GameId = gameId,
            PlayerId = playerId,
            GameMode = GameMode.Daily,
            IsWon = false,
            GuessCount = 6,
            DayNumber = 18,
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(4),
        });

        var stats = await statsStore.LoadAsync();

        Assert.Equal(1, stats.GamesPlayed);
        Assert.Equal(0, stats.GamesWon);
        Assert.Equal(0, stats.CurrentStreak);
        Assert.Equal(0, stats.MaxStreak);
        Assert.Equal(4, stats.ArchiveGamesPlayed);
        Assert.Equal(2, stats.ArchiveGamesWon);
        Assert.Equal(2, stats.ArchiveGamesLost);
    }

    [Fact]
    public async Task LoadAsync_MigratesArchiveCountersForOlderStats()
    {
        var statsStore = new TestPlayerStatsStore(new PlayerStats { Version = 1, Id = Guid.Empty, GuessDistribution = [1, 2, 3] });

        var stats = await statsStore.LoadAsync();

        Assert.Equal(PlayerStats.CurrentVersion, stats.Version);
        Assert.NotEqual(Guid.Empty, stats.Id);
        Assert.Equal(0, stats.ArchiveGamesPlayed);
        Assert.Equal(0, stats.ArchiveGamesWon);
        Assert.Equal(0, stats.ArchiveGamesLost);
        Assert.Equal(6, stats.GuessDistribution.Length);
    }

    [Fact]
    public async Task DateToArchiveDayMapping_UsesFirstDayOrigin()
    {
        var playerId = Guid.NewGuid();
        var statsService = new TestStatsService(playerId);
        var testGameService = new TestGameService();
        var archiveDay = 4;
        var session = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = playerId,
            Mode = Features.Gameplay.Models.GameMode.Daily,
            IsWon = true,
            DailyNumber = archiveDay
        };
        session.FinishedOnUtc = DateTimeOffset.UtcNow;

        var store = new TestGameStatsStore(new[] { session });
        var svc = new ArchiveStatusService(statsService, store, testGameService, new TestArchiveGameStateSource());

        var day = testGameService.GetFirstDay().AddDays(archiveDay - 1);
        var result = await svc.GetStatusesAsync(day, day);

        Assert.Single(result);
        Assert.Equal(DayStatus.Won, result.Values.First());
    }

    [Fact]
    public async Task Uses_LocalStorage_When_Server_Has_No_Status()
    {
        var playerId = Guid.NewGuid();
        var statsService = new TestStatsService(playerId);
        var testGameService = new TestGameService();
        var archiveDay = 5;

        var store = new TestGameStatsStore();
        var localSource = new TestArchiveGameStateSource
        {
            DailyState = new GameState
            {
                GameId = Guid.NewGuid(),
                PlayerId = playerId,
                GameMode = Features.Gameplay.Models.GameMode.Daily,
                Game = new Game { DayNumber = archiveDay, TargetId = 1, PortraitUrl = "test" },
                IsWon = false,
                IsLost = false,
                GuessesMade = [new GuessResult()]
            }
        };

        var svc = new ArchiveStatusService(statsService, store, testGameService, localSource);

        var day = testGameService.GetFirstDay().AddDays(archiveDay - 1);
        var result = await svc.GetStatusesAsync(day, day);

        Assert.Single(result);
        Assert.Equal(DayStatus.InProgress, result.Values.First());
    }

    [Fact]
    public async Task Prefers_More_Advanced_Status_When_Merging()
    {
        var playerId = Guid.NewGuid();
        var statsService = new TestStatsService(playerId);
        var testGameService = new TestGameService();
        var archiveDay = 6;

        var session = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = playerId,
            Mode = Features.Gameplay.Models.GameMode.Daily,
            IsWon = false,
            DailyNumber = archiveDay
        };
        session.StartedOnUtc = DateTimeOffset.UtcNow;

        var store = new TestGameStatsStore(new[] { session });
        var localSource = new TestArchiveGameStateSource
        {
            DailyState = new GameState
            {
                GameId = session.GameId,
                PlayerId = playerId,
                GameMode = Features.Gameplay.Models.GameMode.Daily,
                Game = new Game { DayNumber = archiveDay, TargetId = 1, PortraitUrl = "test" },
                IsWon = true,
                GuessesMade = [new GuessResult()]
            }
        };

        var svc = new ArchiveStatusService(statsService, store, testGameService, localSource);

        var day = testGameService.GetFirstDay().AddDays(archiveDay - 1);
        var result = await svc.GetStatusesAsync(day, day);

        Assert.Single(result);
        Assert.Equal(DayStatus.Won, result.Values.First());
    }

    private sealed class TestArchiveGameStateSource : IArchiveGameStateSource
    {
        public GameState? DailyState { get; init; }
        public GameState? ArchiveState { get; init; }

        public Task<GameState?> LoadAsync(GameMode mode, int dayNumber, CancellationToken cancellationToken = default)
            => Task.FromResult(mode == GameMode.Daily ? DailyState : ArchiveState);
    }

    private sealed class TestGameService : Features.Gameplay.Services.IGameService
    {
        private readonly DateOnly _firstDay = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10);
        public Task<GameState> StartDailyAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<GameState> StartPracticeAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<GameState> StartArchivalAsync(int dayNumber, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> GetTodayDayNumberAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<int> GetMaxArchiveDayAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
        public DateOnly GetFirstDay() => _firstDay;
    }

    private sealed class TestStatsService : IStatisticsService
    {
        private readonly PlayerStats _playerStats;
        public TestStatsService(Guid playerId)
        {
            _playerStats = new PlayerStats { Id = playerId };
        }

        public Task RecordGameStartedAsync(Features.Communication.GameEvents.GameStartedEvent eventData) => throw new NotImplementedException();
        public Task RecordGuessMadeAsync(Features.Communication.GameEvents.GuessMadeEvent eventData) => throw new NotImplementedException();
        public Task RecordGameFinishedAsync(Features.Communication.GameEvents.GameFinishedEvent eventData) => throw new NotImplementedException();
        public Task<PlayerStats> GetPlayerStatsAsync() => Task.FromResult(_playerStats);
        public Task<GameSession> GetGameStatsAsync(Guid gameId) => throw new NotImplementedException();
        public Task<GameSession?> GetPlayerCompletedDailyGameAsync(Guid playerId, int dailyNumber) => throw new NotImplementedException();
        public Task<List<int>> GetPlayerCompletedDailyNumbersAsync(Guid playerId) => throw new NotImplementedException();
    }

    private sealed class TestGameStatsStore : IGameStatsStore
    {
        private readonly List<GameSession> _sessions;
        public int QueryCount { get; private set; }

        public TestGameStatsStore(IEnumerable<GameSession>? sessions = null)
        {
            _sessions = sessions?.ToList() ?? new List<GameSession>();
        }

        public Task<GameSession> LoadOrNewAsync(Guid gameId) => throw new NotImplementedException();
        public Task SaveAsync(GameSession stats) => throw new NotImplementedException();
        public Task<IEnumerable<GameSession>> GetPlayerGames(Guid playerId) => throw new NotImplementedException();
        public Task<GameSession?> GetPlayerCompletedDailyGameAsync(Guid playerId, int dailyNumber) => throw new NotImplementedException();

        public Task<List<GameSession>> GetPlayerDailyGamesByNumberRangeAsync(Guid playerId, int startNumber, int endNumber)
        {
            QueryCount++;

            var list = _sessions
                .Where(s => s.PlayerId == playerId && s.Mode == Features.Gameplay.Models.GameMode.Daily && s.DailyNumber.HasValue)
                .Where(s => s.DailyNumber!.Value >= startNumber && s.DailyNumber!.Value <= endNumber)
                .ToList();

            return Task.FromResult(list);
        }
        public Task<List<int>> GetPlayerCompletedDailyNumbersAsync(Guid playerId) => throw new NotImplementedException();
        public Task<GlobalStatsView> GetGlobalStatsAsync() => throw new NotImplementedException();
        public Task<List<DailyActivePlayersData>> GetDailyActivePlayersAsync(DateOnly? from) => throw new NotImplementedException();
        public Task<List<NewVsReturningPlayersData>> GetNewVsReturningPlayersAsync(DateOnly? from) => throw new NotImplementedException();
        public Task<PlayerActivityDistributionData[]> GetPlayerActivityDistributionAsync() => throw new NotImplementedException();
        public Task<GamesPerPlayerStats> GetGamesPerPlayerStatsAsync() => throw new NotImplementedException();
        public Task<RetentionStats> GetRetentionStatsAsync() => throw new NotImplementedException();
        public Task<GlobalCharacterStats> GetGlobalCharacterStatsAsync() => throw new NotImplementedException();
        public Task<PlayerCharacterStats> GetPlayerCharacterStatsAsync(Guid playerId) => throw new NotImplementedException();
        public Task<DateOnly?> GetPlayerFirstGameDateAsync(Guid playerId) => throw new NotImplementedException();

        public Task<List<GameSession>> GetPlayerDailyGamesInRangeAsync(Guid playerId, DateOnly start, DateOnly end)
        {
            QueryCount++;

            var result = _sessions.Where(s => s.PlayerId == playerId)
                .Where(s =>
                {
                    var d = DateOnly.FromDateTime(s.StartedOnUtc.UtcDateTime);
                    return d >= start && d <= end;
                })
                .ToList();

            return Task.FromResult(result);
        }
    }

    private sealed class TestPlayerStatsStore : IPlayerStatsStore
    {
        private PlayerStats _stats;

        public TestPlayerStatsStore(PlayerStats stats)
        {
            _stats = stats;
        }

        public Task<PlayerStats> LoadAsync() => Task.FromResult(_stats);

        public Task SaveAsync(PlayerStats stats)
        {
            _stats = stats;
            return Task.CompletedTask;
        }

        public Task ClearAsync() => Task.CompletedTask;
    }
}
