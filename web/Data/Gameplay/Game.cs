using QSMPDLE.Web.Models;

namespace QSMPDLE.Web.Data.Gameplay;

public sealed class Game
{
    public int DayNumber { get; set; }
    public Member Target { get; set; } = null!;
    public List<Guess> Guesses { get; } = [];

    public bool IsSolved => Guesses.Any(x => x.IsCorrect);
}
