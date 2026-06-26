using QSMPDLE.Web.Features.Gameplay.Services;

namespace QSMPDLE.Web.Features.Gameplay.Models;

public sealed class Game
{
    public int? DayNumber { get; set; }
    public Character Target { get; set; } = null!;

    public GuessResult VerifyGuess(Character guessed) => CharacterComparer.Compare(Target, guessed);
}
