namespace QSMPDLE.Web.Features.Sharing.Services;

public interface IShareService
{
    ValueTask ShareAsync(string text, string? url = null);
}
