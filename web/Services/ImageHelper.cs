using QSMPDLE.Web.DTOs;
using QSMPDLE.Web.Models;

namespace QSMPDLE.Web.Services;

public static class ImageHelper
{
    public static string GetMemberHead(Member member)
    {
        return $"./graphics/mini-heads/{member.Name}.webp";
    }

    public static string GetMemberHead(MemberLookup member)
    {
        return $"./graphics/mini-heads/{member.Name}.webp";
    }
}
