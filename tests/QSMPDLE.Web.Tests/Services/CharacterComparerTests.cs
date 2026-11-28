using FluentAssertions;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Tests.GameplayFlow;

namespace QSMPDLE.Web.Tests.Services;

public sealed class CharacterComparerTests
{
    private static readonly IReadOnlySet<ClueGroup> NoClues = new HashSet<ClueGroup>();

    [Theory]
    [InlineData("He/Him", "He/Him", ComparisonResult.Correct)]
    [InlineData("He/Him,She/Her", "she/her, He/Him", ComparisonResult.Correct)]
    [InlineData("He/Him", "He/Him,She/Her", ComparisonResult.Partial)]
    [InlineData("He/Him,She/Her", "He/Him", ComparisonResult.Partial)]
    [InlineData("He/Him", "She/Her", ComparisonResult.Wrong)]
    [InlineData("Any", "Any", ComparisonResult.Correct)]
    [InlineData("Any", "He/Him", ComparisonResult.Partial)]
    [InlineData("He/Him", "Any", ComparisonResult.Partial)]
    public async Task Compare_Pronouns_MatchOnlyTheSameSet(string target, string guessed, ComparisonResult expected) =>
        (await CompareAsync(Create(1, target), Create(2, guessed))).Pronouns.Should().Be(expected);

    [Theory]
    [InlineData(2, 2, ComparisonResult.Correct)]
    [InlineData(3, 2, ComparisonResult.More)]
    [InlineData(2, 3, ComparisonResult.Less)]
    [InlineData(4, 7, ComparisonResult.Correct)]
    [InlineData(4, 3, ComparisonResult.More)]
    [InlineData(3, 4, ComparisonResult.Less)]
    public async Task Compare_Languages_TreatFourOrMoreAsOneValue(int target, int guessed, ComparisonResult expected) =>
        (await CompareAsync(Create(1, languages: target), Create(2, languages: guessed))).Languages.Should()
        .Be(expected);

    [Theory]
    [InlineData(5, 5, ComparisonResult.Correct)]
    [InlineData(4, 5, ComparisonResult.Earlier)]
    [InlineData(6, 5, ComparisonResult.Later)]
    public async Task Compare_JoinDay_PointsTowardsTheAnswer(int target, int guessed, ComparisonResult expected) =>
        (await CompareAsync(Create(1, joinDay: target), Create(2, joinDay: guessed))).Joined.Should().Be(expected);

    [Theory]
    [InlineData("", "", ComparisonResult.Correct)]
    [InlineData("", "Guild", ComparisonResult.Wrong)]
    [InlineData("Guild", "", ComparisonResult.Wrong)]
    [InlineData("Guild,Order", "order,guild", ComparisonResult.Correct)]
    [InlineData("Guild,Order", "Guild", ComparisonResult.Partial)]
    [InlineData("Guild", "Guild,Order", ComparisonResult.Partial)]
    [InlineData("Guild", "Order", ComparisonResult.Wrong)]
    public async Task
        Compare_Affiliations_MatchOnlyTheSameSet(string target, string guessed, ComparisonResult expected) =>
        (await CompareAsync(Create(1, affiliations: target), Create(2, affiliations: guessed))).Affiliation.Should()
        .Be(expected);

    [Theory]
    [InlineData("Human", "human", ComparisonResult.Correct)]
    [InlineData("Duck", "Robot", ComparisonResult.Correct)]
    [InlineData("Human", "Duck", ComparisonResult.Wrong)]
    [InlineData("Unknown", "Duck", ComparisonResult.Wrong)]
    [InlineData("Unknown", "Unknown", ComparisonResult.Correct)]
    public async Task Compare_Species_ComparesTheCategory(string target, string guessed, ComparisonResult expected) =>
        (await CompareAsync(Create(1, species: target), Create(2, species: guessed))).Species.Should().Be(expected);

    private static async Task<GuessResult> CompareAsync(Character target, Character guessed) =>
        await new CharacterComparer(new InMemoryCharacterStore([target, guessed], target.Id)).CompareAsync(target.Id,
            guessed.Id, NoClues);

    private static Character Create(int id, string pronouns = "He/Him", int languages = 1, int joinDay = 1,
        string affiliations = "", string species = "Human") =>
        TestCharacters.Create(id, $"Character {id}", joinDay, languages, Split(pronouns), Split(affiliations),
            Split(species));

    private static string[] Split(string values) => values.Split(',', StringSplitOptions.RemoveEmptyEntries);
}
