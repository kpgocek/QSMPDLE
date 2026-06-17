namespace QSMPDLE.Web.Services;

public interface IShareService
{
    ValueTask ShareAsync(string text, string? url = null);
}
