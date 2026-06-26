namespace QSMPDLE.Web.Features.Statistics.Models;

public sealed class GameGuess
{
    public int Id { get; set; }

    public Guid GameId { get; set; }
    public GameSession GameSession { get; set; } = null!;

    public int GuessOrder { get; set; }
    public int GuessedCharacterId { get; set; }
}
