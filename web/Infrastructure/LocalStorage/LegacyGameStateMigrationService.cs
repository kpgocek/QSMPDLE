using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Infrastructure.LocalStorage;

public sealed class LegacyGameStateMigrationService(IGameStateStore gameStateStore) : ILegacyGameStateMigrationService
{
    public async Task MigrateAsync()
    {
        var puzzleIds = await gameStateStore.GetLegacyPuzzleIdsAsync();
        foreach (var puzzleId in puzzleIds)
        {
            var canonical = await gameStateStore.GetByKeyAsync($"canonical-{puzzleId}");
            var daily = await gameStateStore.GetByKeyAsync($"daily-{puzzleId}");
            var archive = await gameStateStore.GetByKeyAsync($"archive-{puzzleId}");

            var selected = new[]
                {
                    CreateCandidate(canonical, puzzleId, isLegacy: false),
                    CreateCandidate(daily, puzzleId, isLegacy: true),
                    CreateCandidate(archive, puzzleId, isLegacy: true),
                }
                .Where(candidate => candidate is not null)
                .Cast<Candidate>()
                .OrderByDescending(candidate => GetProgressPriority(candidate.State))
                .ThenByDescending(candidate => candidate.State.GuessesMade.Count)
                .ThenBy(candidate => candidate.IsLegacy)
                .FirstOrDefault();

            if (selected is not null)
            {
                if (selected.IsLegacy)
                    NormalizeLegacyState(selected.State, puzzleId);

                // The new key is written before any old key is removed.
                await gameStateStore.SaveByKeyAsync($"canonical-{puzzleId}", selected.State);
            }

            // Only remove a verified source after its canonical replacement was written.
            // Keep unreadable values intact rather than silently discarding browser data.
            if (IsCompatibleLegacyState(daily, puzzleId))
                await gameStateStore.RemoveByKeyAsync($"daily-{puzzleId}");
            if (IsCompatibleLegacyState(archive, puzzleId))
                await gameStateStore.RemoveByKeyAsync($"archive-{puzzleId}");
        }
    }

    private static Candidate? CreateCandidate(GameState? state, int puzzleId, bool isLegacy) =>
        state is not null && state.Game.PuzzleId == puzzleId
            ? new Candidate(state, isLegacy)
            : null;

    private static bool IsCompatibleLegacyState(GameState? state, int puzzleId) =>
        state is not null && state.Game.PuzzleId == puzzleId;

    private static void NormalizeLegacyState(GameState state, int puzzleId)
    {
        var entryPoint = state.GameMode == GameMode.Archive ? EntryPoint.Archive : EntryPoint.Daily;
        state.Game.PuzzleId = puzzleId;
        state.SessionCategory = SessionCategory.CanonicalPuzzle;
        state.EntryPoint = entryPoint;
        state.FirstEntryPoint = entryPoint;
    }

    private static int GetProgressPriority(GameState state) => state switch
    {
        { IsWon: true } => 3,
        { IsLost: true } => 2,
        { GuessesMade.Count: > 0 } => 1,
        _ => 0,
    };

    private sealed record Candidate(GameState State, bool IsLegacy);
}
