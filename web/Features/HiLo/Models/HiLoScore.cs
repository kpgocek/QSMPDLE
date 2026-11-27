using System.ComponentModel.DataAnnotations;

namespace QSMPDLE.Web.Features.HiLo.Models;

/// <summary>A player's best run; runs themselves are not stored.</summary>
public sealed class HiLoScore
{
    [Key] public Guid PlayerId { get; init; }

    public int BestScore { get; init; }
}
