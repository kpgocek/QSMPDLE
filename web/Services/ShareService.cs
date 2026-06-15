using Microsoft.JSInterop;

namespace QSMPDLE.Web.Services;

public sealed class ShareService(IJSRuntime js) : IShareService
{
    public async Task ShareAsync(string text)
    {
        await js.InvokeVoidAsync(
            "qsmpdle.share",
            text);
    }

    public async Task ShareToXAsync(string text)
    {
        await js.InvokeVoidAsync(
            "qsmpdle.openTwitter",
            text);
    }

    public async Task ShareToBlueskyAsync(string text)
    {
        await js.InvokeVoidAsync(
            "qsmpdle.openBluesky",
            text);
    }
}
