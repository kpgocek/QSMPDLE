using FluentAssertions;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Sharing;

namespace QSMPDLE.Web.Tests.Services;

public sealed class ShareTextTests
{
    // The template is a raw string literal in a CRLF source file (.gitattributes), so its own breaks are CRLF on
    // every OS; only the emoji rows are joined with Environment.NewLine.
    private const string Crlf = "\r\n";

    private static readonly CharacterLookup Lookup = new(1, "Guess", null, [], "");

    private static readonly GuessResult FiveClueGuess = new()
    {
        Character = Lookup,
        Pronouns = ComparisonResult.Correct,
        Species = ComparisonResult.Wrong,
        Languages = ComparisonResult.More,
        Joined = ComparisonResult.Later,
        Affiliation = ComparisonResult.Partial,
    };

    private static readonly GuessResult SevenClueGuess = new()
    {
        Character = Lookup,
        Pronouns = ComparisonResult.Correct,
        Species = ComparisonResult.Wrong,
        Languages = ComparisonResult.Less,
        Joined = ComparisonResult.Earlier,
        QPhone = ComparisonResult.Wrong,
        Award2026 = ComparisonResult.Correct,
        Affiliation = ComparisonResult.Partial,
    };

    private static readonly GuessResult SevenClueWin = new()
    {
        Character = Lookup,
        IsCorrect = true,
        Pronouns = ComparisonResult.Correct,
        Species = ComparisonResult.Correct,
        Languages = ComparisonResult.Correct,
        Joined = ComparisonResult.Correct,
        Affiliation = ComparisonResult.Correct,
        QPhone = ComparisonResult.Correct,
        Award2026 = ComparisonResult.Correct,
    };

    [Fact]
    public void BuildDailyResult_Win_ListsRowsInGuessOrderAndScoresGuessCount()
    {
        var text = ShareText.BuildDailyResult(42, [FiveClueGuess, SevenClueGuess, SevenClueWin]);

        text.Should().Be(
            "QSMPDLE #42" + Crlf + Crlf
            + "🟩🟥⬆️➡️🟨" + Environment.NewLine
            + "🟩🟥⬇️⬅️🟥🟩🟨" + Environment.NewLine
            + "🟩🟩🟩🟩🟩🟩🟩" + Crlf + Crlf
            + "3/6" + Crlf + Crlf
            + "https://qsmpdle.com");
    }

    [Fact]
    public void BuildDailyResult_Loss_ScoresX()
    {
        var text = ShareText.BuildDailyResult(7, [FiveClueGuess]);

        text.Should().Be(
            "QSMPDLE #7" + Crlf + Crlf
            + "🟩🟥⬆️➡️🟨" + Crlf + Crlf
            + "X/6" + Crlf + Crlf
            + "https://qsmpdle.com");
    }

    [Fact]
    public void BuildDailyChallenge_PinsTheChallengeText()
    {
        var text = ShareText.BuildDailyChallenge(42);

        text.Should().Be(
            "QSMPDLE #42" + Crlf + Crlf
            + "I was unable to beat today's challenge." + Crlf
            + "Will you do better?" + Crlf + Crlf
            + "😭" + Crlf + Crlf
            + "https://qsmpdle.com");
    }
}
