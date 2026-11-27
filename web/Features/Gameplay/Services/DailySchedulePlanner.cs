namespace QSMPDLE.Web.Features.Gameplay.Services;

/// <summary>
/// Chooses upcoming daily characters with the rules the retired schedule generator used: nobody
/// returns within <see cref="RepeatGap"/> days, and the least-scheduled characters go first, so a
/// newly added character catches up without appearing back to back.
/// </summary>
public static class DailySchedulePlanner
{
    public const int RepeatGap = 10;

    /// <summary>Returns one character id per day, starting the day after <paramref name="history"/> ends.</summary>
    /// <param name="characterIds">Every character that may be the answer.</param>
    /// <param name="history">Characters already scheduled, oldest day first.</param>
    /// <param name="count">Number of consecutive days to plan.</param>
    /// <param name="seed">Breaks ties deterministically; the same inputs always give the same schedule.</param>
    /// <param name="isAvailable">
    /// Whether a character (id, day offset from the first planned day) may be picked that day. Unavailable
    /// characters are left out before any counting or random draw, so they never change earlier picks.
    /// </param>
    public static IReadOnlyList<int> Plan(
        IReadOnlyCollection<int> characterIds,
        IReadOnlyList<int> history,
        int count,
        int seed,
        Func<int, int, bool>? isAvailable = null)
    {
        if (characterIds.Count == 0 || count <= 0)
            return [];

        var ids = characterIds.Distinct().Order().ToList();
        var appearances = ids.ToDictionary(id => id, _ => 0);
        foreach (var id in history)
        {
            if (appearances.ContainsKey(id))
                appearances[id]++;
        }

        // A catalog smaller than the gap would otherwise run out of candidates.
        var gap = Math.Min(RepeatGap, ids.Count - 1);
        var recent = new Queue<int>(history.TakeLast(gap));
        var random = new Random(seed);
        var picks = new List<int>(count);

        for (var day = 0; day < count; day++)
        {
            var released = isAvailable is null ? ids : ids.Where(id => isAvailable(id, day)).ToList();
            if (released.Count == 0)
                throw new InvalidOperationException("No character is available to schedule.");

            var available = released.Where(id => !recent.Contains(id)).ToList();
            // A small released pool can be entirely inside the repeat gap; repeating beats having no answer.
            if (available.Count == 0)
                available = released;
            var fewest = available.Min(id => appearances[id]);
            var candidates = available.Where(id => appearances[id] == fewest).ToList();
            var pick = candidates[random.Next(candidates.Count)];

            picks.Add(pick);
            appearances[pick]++;
            recent.Enqueue(pick);
            while (recent.Count > gap)
                recent.Dequeue();
        }

        return picks;
    }
}
