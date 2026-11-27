using QSMPDLE.Web.Features.Archive.Models;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Infrastructure.LocalStorage;
using QSMPDLE.Web.Infrastructure.Persistence;

namespace QSMPDLE.Web.Features.Archive.Services
{
    // Production implementation that queries the game stats store to determine per-day statuses for the current player.
    public sealed class ArchiveStatusService(
        IPlayerStatsStore playerStatsStore,
        IGameStatsStore gameStatsStore,
        QSMPDLE.Web.Features.Gameplay.Services.IDayService dayService,
        IArchiveGameStateSource gameStateSource) : IArchiveStatusService
    {
        private readonly IPlayerStatsStore _playerStatsStore = playerStatsStore ?? throw new ArgumentNullException(nameof(playerStatsStore));
        private readonly IGameStatsStore _gameStatsStore = gameStatsStore ?? throw new ArgumentNullException(nameof(gameStatsStore));
        private readonly QSMPDLE.Web.Features.Gameplay.Services.IDayService _dayService = dayService ?? throw new ArgumentNullException(nameof(dayService));
        private readonly IArchiveGameStateSource _gameStateSource = gameStateSource ?? throw new ArgumentNullException(nameof(gameStateSource));

        public async Task<Dictionary<int, DayStatus>> GetStatusesAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
        {
            var playerId = await _playerStatsStore.GetPlayerIdAsync();

            // Convert the date range into puzzle numbers using DayService's first-day semantics.
            var startNumber = _dayService.GetArchiveDayNumber(start);
            var endNumber = _dayService.GetArchiveDayNumber(end);

            // No known player id, or no archive days in range -> all NotStarted
            if (playerId == Guid.Empty || endNumber < 1)
            {
                return Enumerable.Range(0, (end.DayNumber - start.DayNumber) + 1)
                    .Select((_, i) => start.AddDays(i))
                    .ToDictionary(d => d.DayNumber, _ => DayStatus.NotStarted);
            }

            startNumber = Math.Max(1, startNumber);

            // One canonical session represents the puzzle regardless of whether it was opened
            // through Daily or Archive.
            var sessions = await _gameStatsStore.GetPlayerCanonicalGamesByPuzzleRangeAsync(playerId, startNumber, endNumber);

            var map = new Dictionary<int, DayStatus>();

            // Listing keys once replaces up to four storage reads per calendar day, most of them empty.
            var storedDays = await _gameStateSource.GetStoredDayNumbersAsync(cancellationToken);
            bool MayHaveLocalState(int archiveDay) => storedDays?.Contains(archiveDay) ?? true;

            // Build index by archive day number -> sessions using PuzzleId directly.
            var firstDayNumber = _dayService.GetFirstDay().DayNumber;
            var byDay = sessions
                .Where(s => s.PuzzleId.HasValue)
                .GroupBy(s => firstDayNumber + s.PuzzleId!.Value - 1)
                .ToDictionary(g => g.Key, g => g.ToList());

            // For each calendar day in range, decide status
            for (var d = start; d <= end; d = d.AddDays(1))
            {
                var abs = d.DayNumber;
                if (!byDay.TryGetValue(abs, out var group))
                {
                    map[abs] = MayHaveLocalState(abs - firstDayNumber + 1)
                        ? await GetLocalStorageStatusAsync(abs - firstDayNumber + 1, cancellationToken)
                        : DayStatus.NotStarted;
                    continue;
                }

                // If any session for the day is unfinished -> InProgress
                if (group.Any(s => s.Outcome == GameOutcome.InProgress))
                {
                    map[abs] = DayStatus.InProgress;
                    continue;
                }

                // Among finished sessions, prefer a win if any
                if (group.Any(s => s.Outcome == GameOutcome.Won))
                {
                    map[abs] = DayStatus.Won;
                    continue;
                }

                // Otherwise at least one finished loss
                map[abs] = DayStatus.Lost;

                if (MayHaveLocalState(abs - firstDayNumber + 1))
                {
                    map[abs] = map[abs].MergeWith(await GetLocalStorageStatusAsync(abs - firstDayNumber + 1, cancellationToken));
                }
            }

            return map;
        }

        private async Task<DayStatus> GetLocalStorageStatusAsync(int archiveDayNumber, CancellationToken cancellationToken)
        {
            return GetStatusFromState(await _gameStateSource.LoadAsync(archiveDayNumber, cancellationToken));
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
