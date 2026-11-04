using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Features.Statistics.Services;

public interface IPlayerProfileService
{
    Task<PlayerProfile> GetProfileAsync();
}
