using Microsoft.JSInterop;
using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.Sharing;

public static class ShareText
{
    // The player's name from Profile, when one is set, stands in for "I".
    public static string BuildDailyChallenge(int dayNumber, string? name = null)
    {
        return
            $"""
             QSMPDLE #{dayNumber}

             {name ?? "I"} was unable to beat today's challenge.
             Will you do better?

             😭

             https://qsmpdle.com
             """;
    }

    public static string BuildDailyResult(
        int dayNumber,
        IReadOnlyList<GuessResult> guesses,
        string? name = null)
    {
        var rows = guesses.Select(ToEmoji);

        var result = guesses[guesses.Count - 1].IsCorrect
            ? $"{guesses.Count}/{GameRules.MaxGuesses}"
            : $"X/{GameRules.MaxGuesses}";

        if (name is not null)
        {
            result = $"{name}: {result}";
        }

        return
            $"""
             QSMPDLE #{dayNumber}

             {string.Join(Environment.NewLine, rows)}

             {result}

             https://qsmpdle.com
             """;
    }

    public static string BuildHiLoResult(int score)
    {
        return
            $"""
             QSMPDLE Hi-Lo

             I got {score} in a row.
             Can you beat it?

             https://qsmpdle.com/hi-lo
             """;
    }

    /// <summary>True when the native share sheet handled the text, including a user cancel; false when the browser has none.</summary>
    public static async Task<bool> ShareAsync(IJSRuntime js, string text) =>
        await js.InvokeAsync<bool>("qsmpdleShare.share", text);

    public static Task<bool> CopyAsync(IJSRuntime js, string text) =>
        js.InvokeAsync<bool>("qsmpdleShare.copy", text).AsTask();

    private static string ToEmoji(GuessResult guess) => string.Concat(ToEmoji(guess.Pronouns), ToEmoji(guess.Species),
        ToEmoji(guess.Languages), ToEmoji(guess.Joined), ToEmoji(guess.QPhone), ToEmoji(guess.Award2026),
        ToEmoji(guess.Affiliation));

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
            _ => "⬜"
        };
    }
}
