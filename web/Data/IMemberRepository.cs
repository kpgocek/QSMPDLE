using QSMPDLE.Web.DTOs;

namespace QSMPDLE.Web.Data;

public interface IMemberRepository
{
    Task<List<MemberLookup>> GetLookupAsync();
}
