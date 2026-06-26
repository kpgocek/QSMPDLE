using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Infrastructure.Persistence;

namespace QSMPDLE.Web.Features.Gameplay.Services;

public class CharacterComparer(ICharacterStore CharacterStore) : ICharacterComparer
{
    public async Task<GuessResult> CompareAsync(
        int targetId,
        int guessedId, CancellationToken cancellationToken = default)
    {
        var target = await CharacterStore.GetCharacterAsync(targetId, cancellationToken) ?? throw new ArgumentException("Guessed Id is not connected to character.", nameof(targetId));
        var guessed = await CharacterStore.GetCharacterAsync(guessedId, cancellationToken) ?? throw new ArgumentException("Guessed Id is not connected to character.", nameof(guessedId));
        var guessedLookup = await CharacterStore.GetLookupAsync(guessedId, cancellationToken) ?? throw new ArgumentException("Guessed Id is not connected to character.", nameof(guessedId));

        return new GuessResult
        {
            Character = guessedLookup,
            IsCorrect = guessed.Id == target.Id,

            Pronouns = ComparePronouns(target, guessed),
            Languages = CompareLanguages(target, guessed),
            Joined = CompareJoinDays(target, guessed),
            Affiliation = CompareAffiliations(target, guessed),
            Species = CompareSpecies(target, guessed),
        };
    }

    private static ComparisonResult CompareSpecies(Character target, Character guessed)
    {
        var targetSpecies = GetSpeciesCategory(target.Species.Select(a => a.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase));
        var guessedSpecies = GetSpeciesCategory(guessed.Species.Select(a => a.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase));

        if (targetSpecies == guessedSpecies)
            return ComparisonResult.Correct;

        return ComparisonResult.Wrong;
    }

    private static string GetSpeciesCategory(HashSet<string> hashSet)
    {
        if (hashSet.Contains("Unknown"))
            return "Unknown";
        if (hashSet.Contains("Human"))
            return "Human";

        return "Non-Human";
    }

    private static ComparisonResult CompareAffiliations(Character target, Character guessed)
    {
        var targetAffiliations = target.Affiliations.Select(a => a.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var guessedAffiliations = guessed.Affiliations.Select(a => a.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // TARGET Has No Affiliations
        if (targetAffiliations.Count == 0)
        {
            if (guessedAffiliations.Count == 0)
                return ComparisonResult.Correct;

            return ComparisonResult.Wrong;
        }

        if (targetAffiliations.SetEquals(guessedAffiliations))
            return ComparisonResult.Correct;

        // TARGET Has Affiliations
        if (targetAffiliations.IsSubsetOf(guessedAffiliations))
            return ComparisonResult.Partial;
        if (targetAffiliations.Overlaps(guessedAffiliations))
            return ComparisonResult.Partial;

        return ComparisonResult.Wrong;
    }

    private static ComparisonResult CompareJoinDays(Character target, Character guessed)
    {
        var targetDay = target.JoinDayNumber;
        var guessedDay = guessed.JoinDayNumber;

        if (targetDay < guessedDay)
            return ComparisonResult.Earlier;
        if (targetDay > guessedDay)
            return ComparisonResult.Later;

        return ComparisonResult.Correct;
    }

    private static ComparisonResult CompareLanguages(Character target, Character guessed)
    {
        var targetNumber = target.Languages;
        var guessedNumber = guessed.Languages;

        // LIMIT ON LANGUAGES -> 1, 2, 3, 4+
        var isTargetNumberOverThree = targetNumber > 3;
        var isGuessedNumberOverThree = guessedNumber > 3;

        if (isTargetNumberOverThree && isGuessedNumberOverThree)
            return ComparisonResult.Correct;

        if (targetNumber > guessedNumber)
            return ComparisonResult.More;
        if (targetNumber < guessedNumber)
            return ComparisonResult.Less;

        return ComparisonResult.Correct;
    }

    private static ComparisonResult ComparePronouns(Character target, Character guessed)
    {
        var targetPronouns = target.Pronouns.Select(a => a.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var guessedPronouns = guessed.Pronouns.Select(a => a.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (targetPronouns.Contains("Any"))
        {
            if (guessedPronouns.Contains("Any"))
                return ComparisonResult.Correct;

            return ComparisonResult.Partial;
        }

        if (guessedPronouns.Contains("Any"))
            return ComparisonResult.Partial;

        if (targetPronouns.IsSubsetOf(guessedPronouns))
            return ComparisonResult.Correct;
        if (targetPronouns.Overlaps(guessedPronouns))
            return ComparisonResult.Partial;

        return ComparisonResult.Wrong;
    }
}
