using QSMPDLE.Web.Models;

namespace QSMPDLE.Web.Features.Gameplay.Models;

public sealed class Game
{
    public int DayNumber { get; set; }
    public Character Target { get; set; } = null!;
    public List<Guess> Guesses { get; } = [];

    public bool IsSolved => Guesses.Any(x => x.IsCorrect);
}
