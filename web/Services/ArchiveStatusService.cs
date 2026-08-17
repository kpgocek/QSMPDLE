using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Features.Statistics.Services;
using QSMPDLE.Web.Infrastructure.Persistence;

namespace QSMPDLE.Web.Services
{
    // Production implementation that queries the game stats store to determine per-day statuses for the current player.
    public sealed class ArchiveStatusService : IArchiveStatusService
    {
        private readonly IStatisticsService _statisticsService;
        private readonly IGameStatsStore _gameStatsStore;
        private readonly IGameService _gameService;
        private readonly IArchiveGameStateSource _gameStateSource;

        public ArchiveStatusService(IStatisticsService statisticsService, IGameStatsStore gameStatsStore, IGameService gameService, IArchiveGameStateSource gameStateSource)
        {
            _statisticsService = statisticsService ?? throw new ArgumentNullException(nameof(statisticsService));
            _gameStatsStore = gameStatsStore ?? throw new ArgumentNullException(nameof(gameStatsStore));
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            _gameStateSource = gameStateSource ?? throw new ArgumentNullException(nameof(gameStateSource));
        }

        public async Task<Dictionary<int, DayStatus>> GetStatusesAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
        {
            return await GetStatusesAsync(start, end, includeLocalStorageFallback: true, cancellationToken);
        }

        public async Task<Dictionary<int, DayStatus>> GetStatusesAsync(DateOnly start, DateOnly end, bool includeLocalStorageFallback, CancellationToken cancellationToken = default)
        {
            // Get current player id from statistics service (which uses the same player store as GameStateManager)
            var playerStats = await _statisticsService.GetPlayerStatsAsync();
            var playerId = playerStats?.Id ?? Guid.Empty;

            if (playerId == Guid.Empty)
            {
                // No known player id -> all NotStarted
                return Enumerable.Range(0, (end.DayNumber - start.DayNumber) + 1)
                    .Select((_, i) => start.AddDays(i))
                    .ToDictionary(d => d.DayNumber, _ => DayStatus.NotStarted);
            }

            // Convert the date range into DailyNumber range using GameService first-day semantics.
            var firstDay = _gameService.GetFirstDay();

            var startNumber = start.DayNumber - firstDay.DayNumber + 1;
            var endNumber = end.DayNumber - firstDay.DayNumber + 1;

            if (endNumber < 1 || startNumber > int.MaxValue) // quick bounds check
            {
                // No relevant days in the archive -> all NotStarted
                return Enumerable.Range(0, (end.DayNumber - start.DayNumber) + 1)
                    .Select((_, i) => start.AddDays(i))
                    .ToDictionary(d => d.DayNumber, _ => DayStatus.NotStarted);
            }

            startNumber = Math.Max(1, startNumber);
            endNumber = Math.Max(0, endNumber);

            // Fetch sessions by DailyNumber range (batched)
            var sessions = await _gameStatsStore.GetPlayerDailyGamesByNumberRangeAsync(playerId, startNumber, endNumber);

            var map = new Dictionary<int, DayStatus>();

            // Build index by archive day number -> list of sessions using DailyNumber directly.
            var firstDayNumber = _gameService.GetFirstDay().DayNumber;
            var byDay = sessions
                .Where(s => s.DailyNumber.HasValue)
                .GroupBy(s => firstDayNumber + s.DailyNumber!.Value - 1);

            // For each calendar day in range, decide status
            for (var d = start; d <= end; d = d.AddDays(1))
            {
                var abs = d.DayNumber;
                if (!byDay.Any(g => g.Key == abs))
                {
                    map[abs] = includeLocalStorageFallback
                        ? await GetLocalStorageStatusAsync(abs - firstDayNumber + 1, cancellationToken)
                        : DayStatus.NotStarted;
                    continue;
                }

                var group = byDay.First(g => g.Key == abs);

                // If any session for the day is unfinished -> InProgress
                if (group.Any(s => !s.FinishedOnUtc.HasValue))
                {
                    map[abs] = DayStatus.InProgress;
                    continue;
                }

                // Among finished sessions, prefer a win if any
                if (group.Any(s => s.FinishedOnUtc.HasValue && s.IsWon))
                {
                    map[abs] = DayStatus.Won;
                    continue;
                }

                // Otherwise at least one finished loss
                map[abs] = DayStatus.Lost;

                if (includeLocalStorageFallback)
                {
                    map[abs] = map[abs].MergeWith(await GetLocalStorageStatusAsync(abs, cancellationToken));
                }
            }

            return map;
        }

        private async Task<DayStatus> GetLocalStorageStatusAsync(int archiveDayNumber, CancellationToken cancellationToken)
        {
            var dailyState = await _gameStateSource.LoadAsync(GameMode.Daily, archiveDayNumber, cancellationToken);
            var archiveState = await _gameStateSource.LoadAsync(GameMode.Archive, archiveDayNumber, cancellationToken);

            var dailyGameStatus = GetStatusFromState(dailyState);
            var archiveGameStatus = GetStatusFromState(archiveState);

            return dailyGameStatus.MergeWith(archiveGameStatus);
        }

        private static DayStatus GetStatusFromState(GameState? state)
        {
            if (state is null)
                return DayStatus.NotStarted;

            if (state.IsWon)
                return DayStatus.Won;

            if (state.IsLost)
                return DayStatus.Lost;

            if (!state.IsFinished && state.GuessesMade.Count > 0)
                return DayStatus.InProgress;

            return DayStatus.NotStarted;
        }
    }
}
