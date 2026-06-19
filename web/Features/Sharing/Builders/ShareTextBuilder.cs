using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Features.Sharing.Builders;

public sealed class ShareTextBuilder : IShareTextBuilder
{
    public string BuildDailyChallenge(int dayNumber)
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

    public string BuildDailyResult(
        int dayNumber,
        IReadOnlyList<Guess> guesses)
    {

        var rows = guesses.Select(ToEmoji);

        var result = guesses.LastOrDefault()?.IsCorrect == true
                ? $"{guesses.Count}/6"
                : "X/6";

        return
    $"""
QSMPDLE #{dayNumber}

{string.Join(Environment.NewLine, rows)}

{result}

https://qsmpdle.com
""";
    }

    private string ToEmoji(Guess guess) => string.Join("", ToEmoji(guess.Pronouns), ToEmoji(guess.Species), ToEmoji(guess.Languages), ToEmoji(guess.Affiliation), ToEmoji(guess.Joined));
    private string ToEmoji(ComparisonResult result)
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
