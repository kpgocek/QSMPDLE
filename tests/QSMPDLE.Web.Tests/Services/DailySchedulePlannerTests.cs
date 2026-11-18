using FluentAssertions;
using QSMPDLE.Web.Features.Gameplay.Services;

namespace QSMPDLE.Web.Tests.Services;

public sealed class DailySchedulePlannerTests
{
    private static readonly int[] Catalog = [.. Enumerable.Range(1, 59)];

    [Fact]
    public void Plan_LongRun_NoCharacterRepeatsWithinGap()
    {
        var picks = DailySchedulePlanner.Plan(Catalog, [], 365, seed: 1);

        for (var day = 0; day < picks.Count; day++)
        {
            var window = picks.Skip(Math.Max(0, day - DailySchedulePlanner.RepeatGap)).Take(Math.Min(day, DailySchedulePlanner.RepeatGap));
            window.Should().NotContain(picks[day], $"day {day} repeats a character from the previous {DailySchedulePlanner.RepeatGap} days");
        }
    }

    [Fact]
    public void Plan_ContinuingHistory_AvoidsTheLastScheduledDays()
    {
        int[] history = [.. Enumerable.Range(1, 20)];

        var picks = DailySchedulePlanner.Plan(Catalog, history, 1, seed: 7);

        picks.Single().Should().NotBeInRange(11, 20);
    }

    [Fact]
    public void Plan_UnevenHistory_SchedulesLeastSeenCharactersFirst()
    {
        // Everyone has appeared once except 58 and 59, e.g. characters added after launch.
        int[] history = [.. Enumerable.Range(1, 57)];

        var picks = DailySchedulePlanner.Plan(Catalog, history, 2, seed: 3);

        picks.Should().BeEquivalentTo([58, 59]);
    }

    [Fact]
    public void Plan_SameInputs_SameSchedule()
    {
        int[] history = [4, 8, 15, 16, 23, 42];

        DailySchedulePlanner.Plan(Catalog, history, 30, seed: 201)
            .Should().Equal(DailySchedulePlanner.Plan(Catalog, history, 30, seed: 201));
    }

    [Fact]
    public void Plan_CatalogSmallerThanGap_StillFillsEveryDay()
    {
        var picks = DailySchedulePlanner.Plan([1, 2, 3], [], 9, seed: 5);

        picks.Should().HaveCount(9);
        picks.Zip(picks.Skip(1)).Should().OnlyContain(pair => pair.First != pair.Second);
    }

    [Fact]
    public void Plan_EmptyCatalog_PlansNothing()
    {
        DailySchedulePlanner.Plan([], [], 10, seed: 1).Should().BeEmpty();
    }
}
