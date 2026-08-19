using System.Text.Json.Serialization;

namespace QSMPDLE.Web.Features.Gameplay.Models;

public sealed class GameState()
{
    public required Guid GameId { get; set; }
    public Guid PlayerId { get; set; }
    public required Game Game { get; set; }
    public required GameMode GameMode { get; set; }

    // SchemaVersion is used to detect older cached GameState objects stored in browser local storage.
    // If the persisted version doesn't match the current value the client will invalidate the cache
    // and reload fresh state from the server to avoid showing stale/broken UI.
    public int SchemaVersion { get; set; } = 2;

    public bool IsWon { get; set; }

    public bool IsLost { get; set; }

    public bool SeenPopup { get; set; }

    [JsonIgnore]
    public bool IsFinished => IsWon || IsLost;

    public List<GuessResult> GuessesMade { get; set; } = [];
    public bool StatsRecorded { get; set; }
}
