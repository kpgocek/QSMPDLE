namespace QSMPDLE.Web.Extensions;

public sealed record Results<T>(
    bool Success,
    T? Value = default,
    string? Error = null);

public enum LoadGameResult
{
    LoadedExisting,
    CreatedNew,
    Failed,
}