namespace QSMPDLE.Web.Features.Gameplay.Models;

public sealed class GameState
{
    public bool IsCompleted { get; set; }
    public bool VictoryPopupShown { get; set; }
    public bool FailurePopupShown { get; set; }
    public List<int> GuessesMade { get; set; } = [];
    public bool IsFailed { get; set; }
    public bool StatsRecorded { get; set; }
}
