using Microsoft.JSInterop;

namespace QSMPDLE.Web.Services;

public sealed class ShareService(IJSRuntime js) : IShareService
{
    public async ValueTask ShareAsync(string text, string? url = null)
    {
        await js.InvokeVoidAsync(
            "qsmpdleShare.share",
            text,
            url);
    }
}
