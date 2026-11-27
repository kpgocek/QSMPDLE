using Microsoft.JSInterop;
using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.Sharing;

public static class ShareText
{
    public static string BuildDailyChallenge(int dayNumber)
    {
        return
    $"""
QSMPDLE #{dayNumber}

I was unable to beat today's challenge.
Will you do better?

😭

https://qsmpdle.com
""";
    }

    public static string BuildDailyResult(
        int dayNumber,
        IReadOnlyList<GuessResult> guesses)
    {

        var rows = guesses.Select(ToEmoji);

        var result = guesses[guesses.Count - 1].IsCorrect
                ? $"{guesses.Count}/{GameRules.MaxGuesses}"
                : $"X/{GameRules.MaxGuesses}";

        return
    $"""
QSMPDLE #{dayNumber}

{string.Join(Environment.NewLine, rows)}

{result}

https://qsmpdle.com
""";
    }

    /// <summary>True when the native share sheet handled the text, including a user cancel; false when the browser has none.</summary>
    public static async Task<bool> ShareAsync(IJSRuntime js, string text) =>
        await js.InvokeAsync<bool>("qsmpdleShare.share", text);

    public static Task<bool> CopyAsync(IJSRuntime js, string text) =>
        js.InvokeAsync<bool>("qsmpdleShare.copy", text).AsTask();

    private static string ToEmoji(GuessResult guess) => string.Concat(ToEmoji(guess.Pronouns), ToEmoji(guess.Species), ToEmoji(guess.Languages), ToEmoji(guess.Joined), ToEmoji(guess.QPhone), ToEmoji(guess.Award2026), ToEmoji(guess.Affiliation));

    // A clue released after the puzzle's date has no result and no square.
    private static string ToEmoji(ComparisonResult? result) => result is { } value ? ToEmoji(value) : string.Empty;

    private static string ToEmoji(ComparisonResult result)
    {
        return result switch
        {
            ComparisonResult.Correct => "🟩",
            ComparisonResult.Earlier => "⬅️",
            ComparisonResult.Later => "➡️",
            ComparisonResult.Less => "⬇️",
            ComparisonResult.More => "⬆️",
            ComparisonResult.Partial => "🟨",
            ComparisonResult.Wrong => "🟥",
            _ => "⬜",
        };
    }
}
