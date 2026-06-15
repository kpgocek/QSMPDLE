using MudBlazor;
using QSMPDLE.Web.Data.Gameplay;

namespace QSMPDLE.Web.Services;

public static class CssHelper
{
    public static Color ToColor(ComparisonResult result)
    {
        return result switch
        {
            ComparisonResult.Correct => Color.Success,
            ComparisonResult.Partial => Color.Warning,
            ComparisonResult.Wrong => Color.Error,

            ComparisonResult.Earlier or
            ComparisonResult.Later or
            ComparisonResult.More or
            ComparisonResult.Less
                => Color.Primary,

            _ => Color.Default
        };
    }
}
