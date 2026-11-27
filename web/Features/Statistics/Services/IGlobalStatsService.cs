using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Features.Statistics.Services;

/// <summary>Site-wide statistics shared by every visitor; never holds per-player data.</summary>
public interface IGlobalStatsService
{
    Task<GlobalTotals> GetTotalsAsync();
}
