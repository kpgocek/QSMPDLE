using FluentAssertions;
using Microsoft.Extensions.Options;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Features.HiLo.Models;
using QSMPDLE.Web.Features.HiLo.Services;
using QSMPDLE.Web.Tests.GameplayFlow;

namespace QSMPDLE.Web.Tests.Services;

public sealed class HiLoServiceTests
{
    private static readonly DateOnly Premiere = new(2026, 12, 1);

    private readonly Character[] characters =
    [
        new()
        {
            Id = 1,
            Name = "All",
            JoinDayNumber = 1,
            PhoneNumber = 768,
            YouTubeSubscribers = 10,
            TwitchFollowers = 10,
            KickFollowers = 10
        },
        new()
        {
            Id = 2,
            Name = "Zeros",
            JoinDayNumber = 1,
            PhoneNumber = 444,
            YouTubeSubscribers = 20,
            TwitchFollowers = 0,
            KickFollowers = 0
        },
        new() { Id = 3, Name = "Nulls", JoinDayNumber = 5 },
        new()
        {
            Id = 60,
            Name = "Unreleased",
            JoinDayNumber = 125,
            TwitchFollowers = 5,
            KickFollowers = 5,
            AvailableFrom = Premiere
        }
    ];

    private readonly TestClock clock = new(new DateTimeOffset(2026, 11, 30, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task CreateRoundAsync_BeforePremiere_UsesOnlyReleasedCharactersWithTheStat()
    {
        var rounds = await CreateRoundsAsync();

        rounds.Should().OnlyContain(round =>
            round.Known.Id != round.Hidden.Id && round.KnownValue > 0 && round.HiddenValue > 0);
        rounds.Should().NotContain(round => round.Known.Id == 60 || round.Hidden.Id == 60);
        rounds.Select(round => round.Stat).Distinct().Should().BeEquivalentTo(
            [HiLoStat.DayArrived, HiLoStat.YouTubeSubscribers],
            "the other stats have one released character, or wait for the QPhone premiere");
    }

    [Fact]
    public async Task CreateRoundAsync_OnPremiere_AddsQPhonesAndNewCharacters()
    {
        clock.Now = new DateTimeOffset(Premiere, TimeOnly.MinValue, TimeSpan.Zero);

        var rounds = await CreateRoundsAsync();

        rounds.Select(round => round.Stat).Distinct().Should().BeEquivalentTo(Enum.GetValues<HiLoStat>());
        rounds.Should().Contain(round => round.Known.Id == 60);
    }

    [Fact]
    public async Task CreateRoundAsync_AfterARound_CarriesTheHiddenCharacterOver()
    {
        var service = CreateService();
        var previous = await service.CreateRoundAsync();

        for (var count = 0; count < 500; count++)
        {
            var next = await service.CreateRoundAsync(previous);

            next.Known.Should().BeSameAs(previous.Hidden);
            next.Hidden.Id.Should().NotBe(previous.Known.Id).And.NotBe(previous.Hidden.Id);
            next.KnownValue.Should().Be(next.Stat.GetValue(next.Known));
            next.HiddenValue.Should().Be(next.Stat.GetValue(next.Hidden));
            previous = next;
        }
    }

    [Fact]
    public async Task CreateRoundAsync_NobodyLeftToCompareWith_StartsAFreshPair()
    {
        var service = CreateService(characters[..2]);
        var previous = await service.CreateRoundAsync();

        var next = await service.CreateRoundAsync(previous);

        next.Known.Id.Should().NotBe(next.Hidden.Id);
    }

    [Theory]
    [InlineData(5, 4, HiLoAnswer.Lower)]
    [InlineData(5, 5, HiLoAnswer.Equal)]
    [InlineData(5, 6, HiLoAnswer.Higher)]
    public void Answer_HiddenValue_IsComparedWithTheKnownOne(int known, int hidden, HiLoAnswer expected) =>
        new HiLoRound(HiLoStat.DayArrived, characters[0], known, characters[1], hidden).Answer.Should().Be(expected);

    private async Task<List<HiLoRound>> CreateRoundsAsync()
    {
        var service = CreateService();

        var rounds = new List<HiLoRound>();
        for (var round = 0; round < 500; round++)
        {
            rounds.Add(await service.CreateRoundAsync());
        }

        return rounds;
    }

    private HiLoService CreateService(Character[]? pool = null)
    {
        var options = Options.Create(new GameplayOptions { CluePremieres = { [ClueGroup.QPhone] = Premiere } });
        return new HiLoService(
            new InMemoryCharacterStore(pool ?? characters, 1),
            new GameplayAvailabilityService(new DayService(clock), options),
            new InMemoryPlayerStatsStore(),
            new InMemoryHiLoScoreStore());
    }
}
