using System.ComponentModel.DataAnnotations.Schema;

namespace QSMPDLE.Web.Models;

public sealed class DailyGame
{
    [Column("id")]
    public int Id { get; set; }

    [Column("date")]
    public DateOnly Date { get; set; }

    [Column("character_id")]
    public int CharacterId { get; set; }

    public Character Character { get; set; } = null!;
}
