using System.Text.Json.Serialization;

namespace QSMPDLE.Web.Features.Gameplay.Models;

public sealed class GameState()
{
    public required Guid GameId { get; set; }
    public Guid PlayerId { get; set; }
    public required Game Game { get; set; }

    public bool IsWon { get; set; }

    public bool IsLost { get; set; }

    [JsonIgnore]
    public bool IsFinished => IsWon || IsLost;

    public List<GuessResult> GuessesMade { get; set; } = [];
    public bool StatsRecorded { get; set; }
}
