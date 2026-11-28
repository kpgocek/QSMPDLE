using FluentAssertions;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Tests.GameplayFlow;

namespace QSMPDLE.Web.Tests.Services;

public sealed class GameSessionTests
{
    private const int Target = 100;
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RecordGuesses_CorrectGuess_FinishesAsWon()
    {
        var session = Play(1, Target);

        session.GuessedCharacterIds.Should().Equal(1, Target);
        session.Guesses.Should().OnlyContain(guess => guess.GuessedOnUtc == Now);
        session.IsWon.Should().BeTrue();
        session.FinishedOnUtc.Should().Be(Now);
    }

    [Fact]
    public void RecordGuesses_SixthWrongGuess_FinishesAsLost()
    {
        var session = Play(1, 2, 3, 4, 5, 6);

        session.IsWon.Should().BeFalse();
        session.FinishedOnUtc.Should().Be(Now);
    }

    [Fact]
    public void RecordGuesses_SixthCorrectGuess_FinishesAsWon() =>
        Play(1, 2, 3, 4, 5, Target).IsWon.Should().BeTrue();

    [Fact]
    public void RecordGuesses_SameBoardTwice_ChangesNothing()
    {
        var session = Play(1, 2);

        session.RecordGuesses([1, 2], Now.AddMinutes(1));

        session.GuessedCharacterIds.Should().Equal(1, 2);
        session.FinishedOnUtc.Should().BeNull();
    }

    [Theory]
    [InlineData(new[] { 9 })]
    [InlineData(new[] { 2, 1, 3 })]
    [InlineData(new[] { 1, 9, 3 })]
    public void RecordGuesses_BoardFromAStaleTab_ChangesNothing(int[] board)
    {
        var session = Play(1, 2);

        session.RecordGuesses(board, Now);

        session.GuessedCharacterIds.Should().Equal(1, 2);
    }

    [Theory]
    [InlineData(new[] { 1, Target }, 3)]
    [InlineData(new[] { 1, 2, 3, 4, 5, 6 }, 7)]
    public void RecordGuesses_DecidedGame_KeepsItsFirstResult(int[] board, int extraGuess)
    {
        var session = Play(board);

        session.RecordGuesses([.. board, extraGuess], Now.AddMinutes(1));

        session.GuessedCharacterIds.Should().Equal(board);
        session.FinishedOnUtc.Should().Be(Now);
    }

    [Theory]
    [InlineData(Target, false, false, new int[0], GameOutcome.InProgress)]
    [InlineData(Target, false, false, new[] { 1, 2, 3, 4, 5 }, GameOutcome.InProgress)]
    [InlineData(Target, false, false, new[] { 1, 2, 3, 4, 5, 6 }, GameOutcome.Lost)]
    [InlineData(Target, false, false, new[] { 1, 2, 3, 4, 5, Target }, GameOutcome.Won)]
    [InlineData(0, false, false, new[] { 1, 2, 3, 4, 5, 6 }, GameOutcome.InProgress)]
    [InlineData(0, true, false, new[] { 1 }, GameOutcome.Lost)]
    [InlineData(0, true, true, new[] { 1 }, GameOutcome.Won)]
    public void Outcome_StoredSession_UsesTheSavedResultAndTheGuesses(int target, bool finished, bool isWon,
        int[] board, GameOutcome expected)
    {
        var session = new GameSession
        {
            GameId = Guid.NewGuid(),
            TargetCharacterId = target,
            IsWon = isWon,
            FinishedOnUtc = finished ? Now : null
        };
        foreach (var characterId in board)
        {
            session.AddGuess(characterId);
        }

        session.Outcome.Should().Be(expected);
        GameSession.IsDecided.Compile()(session).Should().Be(expected != GameOutcome.InProgress);
    }

    private static GameSession Play(params int[] board)
    {
        var session = new GameSession { GameId = Guid.NewGuid(), TargetCharacterId = Target };
        for (var count = 1; count <= board.Length; count++)
        {
            session.RecordGuesses([.. board.Take(count)], Now);
        }

        return session;
    }
}
