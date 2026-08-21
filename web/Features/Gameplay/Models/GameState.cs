using System.Text.Json.Serialization;

namespace QSMPDLE.Web.Features.Gameplay.Models;

public sealed class GameState()
{
    public required Guid GameId { get; set; }
    public Guid PlayerId { get; set; }
    public required Game Game { get; set; }
    public SessionCategory SessionCategory { get; set; } = SessionCategory.CanonicalPuzzle;
    public EntryPoint EntryPoint { get; set; } = EntryPoint.Daily;
    public EntryPoint FirstEntryPoint { get; set; } = EntryPoint.Daily;

    // Kept only to deserialize browser states written by the previous release.
    // New application code must use SessionCategory and EntryPoint.
    public GameMode GameMode { get; set; }

    // SchemaVersion is used to detect older cached GameState objects stored in browser local storage.
    // If the persisted version doesn't match the current value the client will invalidate the cache
    // and reload fresh state from the server to avoid showing stale/broken UI.
    public int SchemaVersion { get; set; } = 3;

    public bool IsWon { get; set; }

    public bool IsLost { get; set; }

    public bool SeenPopup { get; set; }

    [JsonIgnore]
    public bool IsFinished => IsWon || IsLost;

    public List<GuessResult> GuessesMade { get; set; } = [];
    public bool StatsRecorded { get; set; }
}
