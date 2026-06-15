namespace QSMPDLE.Web.Models;

public sealed class DailyGame
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    public int MemberId { get; set; }

    public Member Member { get; set; } = null!;
}
