using QSMPDLE.Web.Data.Gameplay;
using QSMPDLE.Web.Models;

namespace QSMPDLE.Web.Services;

public static class MemberComparer
{
    public static Guess Compare(
        Member target,
        Member guessed)
    {
        return new Guess
        {
            Member = guessed,
            IsCorrect = guessed.Id == target.Id,

            Pronouns = ComparePronouns(target, guessed),
            Languages = CompareLanguages(target, guessed),
            Joined = CompareJoinDays(target, guessed),
            Affiliation = CompareAffiliations(target, guessed),
            Species = CompareSpecies(target, guessed),
        };
    }

    private static ComparisonResult CompareSpecies(Member target, Member guessed)
    {
        var targetSpecies = GetSpeciesCategory(target.Species.Select(a => a.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase));
        var guessedSpecies = GetSpeciesCategory(guessed.Species.Select(a => a.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase));

        if (targetSpecies == guessedSpecies) return ComparisonResult.Correct;

        return ComparisonResult.Wrong;
    }

    private static string GetSpeciesCategory(HashSet<string> hashSet)
    {
        if (hashSet.Contains("Unknown")) return "Unknown";
        if (hashSet.Contains("Human")) return "Human";

        return "Non-Human";
    }

    private static ComparisonResult CompareAffiliations(Member target, Member guessed)
    {
        var targetAffiliations = target.Affiliations.Select(a => a.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var guessedAffiliations = guessed.Affiliations.Select(a => a.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // TARGET Has No Affiliations
        if (targetAffiliations.Count == 0)
        {
            if (guessedAffiliations.Count == 0) return ComparisonResult.Correct;

            return ComparisonResult.Wrong;
        }

        if (targetAffiliations.SetEquals(guessedAffiliations)) return ComparisonResult.Correct;

        // TARGET Has Affiliations
        if (targetAffiliations.IsSubsetOf(guessedAffiliations)) return ComparisonResult.Partial;
        if (targetAffiliations.Overlaps(guessedAffiliations)) return ComparisonResult.Partial;

        return ComparisonResult.Wrong;
    }

    private static ComparisonResult CompareJoinDays(Member target, Member guessed)
    {
        var targetDay = target.JoinDayNumber;
        var guessedDay = guessed.JoinDayNumber;

        if (targetDay < guessedDay) return ComparisonResult.Earlier;
        if (targetDay > guessedDay) return ComparisonResult.Later;

        return ComparisonResult.Correct;
    }

    private static ComparisonResult CompareLanguages(Member target, Member guessed)
    {
        var targetNumber = target.Languages;
        var guessedNumber = guessed.Languages;

        // LIMIT ON LANGUAGES -> 1, 2, 3, 4+
        var isTargetNumberOverThree = targetNumber > 3;
        var isGuessedNumberOverThree = guessedNumber > 3;

        if (isTargetNumberOverThree && isGuessedNumberOverThree) return ComparisonResult.Correct;

        if (targetNumber > guessedNumber) return ComparisonResult.More;
        if (targetNumber < guessedNumber) return ComparisonResult.Less;

        return ComparisonResult.Correct;
    }

    private static ComparisonResult ComparePronouns(Member target, Member guessed)
    {
        var targetPronouns = target.Pronouns.Select(a => a.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var guessedPronouns = guessed.Pronouns.Select(a => a.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (targetPronouns.Contains("Any"))
        {
            if (guessedPronouns.Contains("Any")) return ComparisonResult.Correct;

            return ComparisonResult.Partial;
        }

        if (targetPronouns.IsSubsetOf(guessedPronouns)) return ComparisonResult.Correct;
        if (targetPronouns.Overlaps(guessedPronouns)) return ComparisonResult.Partial;

        return ComparisonResult.Wrong;
    }
}
