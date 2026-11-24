using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace QSMPDLE.Web.Tests.Infrastructure;

/// <summary>A browser whose storage calls complete asynchronously and whose messages are limited, as over a circuit.</summary>
internal sealed class FakeBrowser : IJSRuntime
{
    // SignalR's default; a larger result closes the connection.
    private const int MaxMessageBytes = 32 * 1024;

    public Dictionary<string, string> Items { get; } = [];
    public int FailuresLeft { get; set; }

    public ILocalStorageService LocalStorage => new ServiceCollection()
        .AddLocalStorageServices()
        .AddSingleton<IJSRuntime>(this)
        .BuildServiceProvider()
        .GetRequiredService<ILocalStorageService>();

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        await Task.Delay(10, cancellationToken);
        if (FailuresLeft-- > 0)
            throw new JSException("Simulated storage failure.");

        if (identifier == "storageKeys")
            return Send<TValue>(Keys((string)args![0]!));

        var key = (string)args![0]!;
        if (identifier.EndsWith("setItem", StringComparison.Ordinal))
        {
            Items[key] = (string)args[1]!;
            return default!;
        }

        return Send<TValue>(Items.GetValueOrDefault(key));
    }

    // Mirrors wwwroot/js/storage.js.
    private string[] Keys(string prefix) =>
        [.. Items.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).Select(key => key[prefix.Length..])];

    private static TValue Send<TValue>(object? result) =>
        JsonSerializer.SerializeToUtf8Bytes(result).Length <= MaxMessageBytes
            ? (TValue)result!
            : throw new JSDisconnectedException("The message is larger than the connection allows.");
}
