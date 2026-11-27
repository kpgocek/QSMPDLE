using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.HiLo.Models;

public sealed record HiLoRound(HiLoStat Stat, Character Known, int KnownValue, Character Hidden, int HiddenValue)
{
    /// <summary>How the hidden value relates to the known one.</summary>
    public HiLoAnswer Answer => HiddenValue == KnownValue ? HiLoAnswer.Equal
        : HiddenValue > KnownValue ? HiLoAnswer.Higher
        : HiLoAnswer.Lower;
}
