using FluentAssertions;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Features.Statistics.Services;

namespace QSMPDLE.Web.Tests.GameplayFlow;

public sealed class GameStateManagerStatisticsTests
{
    [Fact]
    public async Task GameStartedStatisticIsRecordedAtTheFirstValidGuessPoint()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Daily);

        await setup.Manager.MakeGuessAsync(setup.IncorrectGuess.Id);

        setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId].StartedOnUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task ValidGuessProducesExpectedPersistedStatisticsState()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Daily);

        await setup.Manager.MakeGuessAsync(setup.IncorrectGuess.Id);

        var session = setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId];
        session.Guesses.Should().ContainSingle();
        session.Guesses.First().GuessedCharacterId.Should().Be(setup.IncorrectGuess.Id);
        setup.PlayerStatsStore.Stats.LastPlayedDailyGameId.Should().Be(setup.Manager.GameState.GameId);
    }

    [Fact]
    public async Task WinningCompletionIsRecorded()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Daily);

        await setup.Manager.MakeGuessAsync(setup.Target.Id);

        var session = setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId];
        session.FinishedOnUtc.Should().NotBeNull();
        session.IsWon.Should().BeTrue();
        setup.PlayerStatsStore.Stats.GamesWon.Should().Be(1);
        setup.PlayerStatsStore.Stats.GamesPlayed.Should().Be(1);
    }

    [Fact]
    public async Task LosingCompletionIsRecorded()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Daily);

        foreach (var guess in setup.LosingGuesses)
        {
            await setup.Manager.MakeGuessAsync(guess.Id);
        }

        var session = setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId];
        session.FinishedOnUtc.Should().NotBeNull();
        session.IsWon.Should().BeFalse();
        session.Guesses.Should().HaveCount(6);
        setup.PlayerStatsStore.Stats.GamesPlayed.Should().Be(1);
        setup.PlayerStatsStore.Stats.GamesWon.Should().Be(0);
    }

    [Fact]
    public async Task UnfinishedGameDoesNotProduceCompletionStatistics()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Daily);

        await setup.Manager.MakeGuessAsync(setup.IncorrectGuess.Id);

        setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId].FinishedOnUtc.Should().BeNull();
        setup.PlayerStatsStore.Stats.GamesPlayed.Should().Be(0);
    }

    [Fact]
    public async Task DuplicateRejectedGuessDoesNotProduceAdditionalGuessStatistics()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Daily);

        await setup.Manager.MakeGuessAsync(setup.IncorrectGuess.Id);
        await setup.Manager.MakeGuessAsync(setup.IncorrectGuess.Id);

        setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId].Guesses.Should().ContainSingle();
    }

    [Fact]
    public async Task CompletionRecordingRemainsIdempotent()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Daily);

        await setup.Manager.MakeGuessAsync(setup.Target.Id);
        await setup.Manager.MarkStatsRecordedAsync();
        await setup.Manager.MarkStatsRecordedAsync();

        setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId].FinishedOnUtc.Should().NotBeNull();
        setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId].IsWon.Should().BeTrue();
        setup.Manager.GameState.StatsRecorded.Should().BeTrue();
    }

    [Fact]
    public async Task DailyPlayerStatsAreUpdatedAccordingToCurrentSemantics()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Daily);

        await setup.Manager.MakeGuessAsync(setup.Target.Id);

        var dayNumber = setup.Manager.GameState.Game.DayNumber;

        setup.PlayerStatsStore.Stats.GamesPlayed.Should().Be(1);
        setup.PlayerStatsStore.Stats.GamesWon.Should().Be(1);
        setup.PlayerStatsStore.Stats.LastCompletedDayNumber.Should().Be(dayNumber);
        setup.PlayerStatsStore.Stats.LastPlayedDailyGameId.Should().Be(setup.Manager.GameState.GameId);
        setup.PlayerStatsStore.Stats.GuessDistribution[0].Should().Be(1);
    }

    [Fact]
    public async Task GameSessionContainsStartFinishWinLossAndGuesses()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Daily);

        await setup.Manager.MakeGuessAsync(setup.Target.Id);

        var session = setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId];
        var dayNumber = setup.Manager.GameState.Game.DayNumber;

        session.PlayerId.Should().Be(setup.PlayerStatsStore.Stats.Id);
        session.Mode.Should().Be(GameMode.Daily);
        session.DailyNumber.Should().Be(dayNumber);
        session.TargetCharacterId.Should().Be(setup.Target.Id);
        session.FinishedOnUtc.Should().NotBeNull();
        session.Guesses.Should().ContainSingle();
        session.Guesses.First().GuessedCharacterId.Should().Be(setup.Target.Id);
    }

    [Fact]
    public async Task DailyAndArchiveUseTheSamePlayerIdWhenPlayerStatsAreIntact()
    {
        var setup = CreateSetup();

        await setup.Manager.StartGameAsync(GameMode.Daily);
        setup.Manager.GameState.PlayerId.Should().Be(setup.PlayerStatsStore.Stats.Id);

        await setup.Manager.MakeGuessAsync(setup.Target.Id);

        var session = setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId];
        session.PlayerId.Should().Be(setup.PlayerStatsStore.Stats.Id);

        await setup.Manager.StartGameAsync(GameMode.Archive, setup.Manager.GameState.Game.DayNumber);

        setup.Manager.GameState.PlayerId.Should().Be(setup.PlayerStatsStore.Stats.Id);
    }

    [Fact]
    public async Task CompletedDailyWinAppearsInHistoryForSamePlayerId()
    {
        var setup = CreateSetup();

        await setup.Manager.StartGameAsync(GameMode.Daily);
        await setup.Manager.MakeGuessAsync(setup.Target.Id);

        var completedDay = setup.Manager.GameState.Game.DayNumber;
        completedDay.Should().NotBeNull();
        var completedDayValue = completedDay.Value;
        var numbers = await setup.StatisticsService.GetPlayerCompletedDailyNumbersAsync(setup.PlayerStatsStore.Stats.Id);

        numbers.Should().ContainSingle().Which.Should().Be(completedDayValue);
        (await setup.StatisticsService.GetPlayerCompletedDailyGameAsync(setup.PlayerStatsStore.Stats.Id, completedDayValue)).Should().NotBeNull();
    }

    [Fact]
    public async Task CompletedDailyLossAppearsInHistoryForSamePlayerId()
    {
        var setup = CreateSetup();

        await setup.Manager.StartGameAsync(GameMode.Daily);

        foreach (var guess in setup.LosingGuesses)
        {
            await setup.Manager.MakeGuessAsync(guess.Id);
        }

        var completedDay = setup.Manager.GameState.Game.DayNumber;
        completedDay.Should().NotBeNull();
        var completedDayValue = completedDay.Value;
        var numbers = await setup.StatisticsService.GetPlayerCompletedDailyNumbersAsync(setup.PlayerStatsStore.Stats.Id);

        numbers.Should().ContainSingle().Which.Should().Be(completedDayValue);
        (await setup.StatisticsService.GetPlayerCompletedDailyGameAsync(setup.PlayerStatsStore.Stats.Id, completedDayValue)).Should().NotBeNull();
    }

    [Fact]
    public async Task UnfinishedDailyDoesNotAppearInHistory()
    {
        var setup = CreateSetup();

        await setup.Manager.StartGameAsync(GameMode.Daily);
        await setup.Manager.MakeGuessAsync(setup.IncorrectGuess.Id);

        var completedDay = setup.Manager.GameState.Game.DayNumber;
        completedDay.Should().NotBeNull();
        var completedDayValue = completedDay.Value;
        var numbers = await setup.StatisticsService.GetPlayerCompletedDailyNumbersAsync(setup.PlayerStatsStore.Stats.Id);

        numbers.Should().BeEmpty();
        (await setup.StatisticsService.GetPlayerCompletedDailyGameAsync(setup.PlayerStatsStore.Stats.Id, completedDayValue)).Should().BeNull();
    }

    [Fact]
    public async Task DifferentPlayerIdDoesNotSeeAnotherPlayersHistory()
    {
        var setup = CreateSetup();
        var playerId = setup.PlayerStatsStore.Stats.Id;

        await setup.Manager.StartGameAsync(GameMode.Daily);
        await setup.Manager.MakeGuessAsync(setup.Target.Id);

        var otherPlayerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        setup.PlayerStatsStore.Stats = new PlayerStats { Id = otherPlayerId };
        var completedDayValue = setup.Manager.GameState.Game.DayNumber!.Value;

        (await setup.StatisticsService.GetPlayerCompletedDailyNumbersAsync(otherPlayerId)).Should().BeEmpty();
        (await setup.StatisticsService.GetPlayerCompletedDailyGameAsync(otherPlayerId, completedDayValue)).Should().BeNull();
        (await setup.StatisticsService.GetPlayerCompletedDailyNumbersAsync(playerId)).Should().ContainSingle();
    }

    [Fact]
    public async Task ArchiveDoesNotChangeDailyStreakOrProgression()
    {
        var setup = CreateSetup();

        await setup.Manager.StartGameAsync(GameMode.Daily);
        await setup.Manager.MakeGuessAsync(setup.Target.Id);

        var beforeStats = new PlayerStats
        {
            Version = setup.PlayerStatsStore.Stats.Version,
            Id = setup.PlayerStatsStore.Stats.Id,
            GamesPlayed = setup.PlayerStatsStore.Stats.GamesPlayed,
            GamesWon = setup.PlayerStatsStore.Stats.GamesWon,
            CurrentStreak = setup.PlayerStatsStore.Stats.CurrentStreak,
            MaxStreak = setup.PlayerStatsStore.Stats.MaxStreak,
            LastCompletedDayNumber = setup.PlayerStatsStore.Stats.LastCompletedDayNumber,
            LastPlayedDailyGameId = setup.PlayerStatsStore.Stats.LastPlayedDailyGameId,
            GuessDistribution = setup.PlayerStatsStore.Stats.GuessDistribution.ToArray(),
        };
        await setup.Manager.StartGameAsync(GameMode.Archive, setup.Manager.GameState.Game.DayNumber!.Value);

        setup.PlayerStatsStore.Stats.GamesPlayed.Should().Be(beforeStats.GamesPlayed);
        setup.PlayerStatsStore.Stats.GamesWon.Should().Be(beforeStats.GamesWon);
        setup.PlayerStatsStore.Stats.CurrentStreak.Should().Be(beforeStats.CurrentStreak);
        setup.PlayerStatsStore.Stats.LastCompletedDayNumber.Should().Be(beforeStats.LastCompletedDayNumber);
    }

    [Fact]
    public async Task ArchiveCompletedDayHistoryIsLoadedInOneQuery()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Daily);
        await setup.Manager.MakeGuessAsync(setup.Target.Id);

        var playerId = setup.PlayerStatsStore.Stats.Id;
        var numbers = await setup.StatisticsService.GetPlayerCompletedDailyNumbersAsync(playerId);

        numbers.Should().ContainSingle();
    }

    [Fact]
    public async Task ArchiveRecognizesCompletedDailyWin()
    {
        var setup = CreateSetup();
        var completedDaily = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = setup.PlayerStatsStore.Stats.Id,
            Mode = GameMode.Daily,
            DailyNumber = 3,
            TargetCharacterId = setup.Target.Id,
            FinishedOnUtc = DateTimeOffset.UtcNow,
            IsWon = true,
            Guesses = [new GameGuess { GameId = Guid.NewGuid(), GuessOrder = 0, GuessedCharacterId = setup.Target.Id }]
        };
        setup.GameStatsStore.Sessions[completedDaily.GameId] = completedDaily;

        var result = await setup.Manager.StartGameAsync(GameMode.Archive, 3);

        result.Should().Be(Extensions.LoadGameResult.LoadedExisting);
        setup.Manager.GameState.GameMode.Should().Be(GameMode.Archive);
        setup.Manager.GameState.GameId.Should().NotBe(Guid.Empty);
        setup.Manager.GameState.IsWon.Should().BeTrue();
        setup.Manager.GameState.IsFinished.Should().BeTrue();
        setup.Manager.GameState.Game.DayNumber.Should().Be(3);
    }

    [Fact]
    public async Task ArchiveRecognizesCompletedDailyLoss()
    {
        var setup = CreateSetup();
        var completedDaily = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = setup.PlayerStatsStore.Stats.Id,
            Mode = GameMode.Daily,
            DailyNumber = 3,
            TargetCharacterId = setup.Target.Id,
            FinishedOnUtc = DateTimeOffset.UtcNow,
            IsWon = false,
            Guesses = Enumerable.Range(0, 6).Select(index => new GameGuess { GameId = Guid.NewGuid(), GuessOrder = index, GuessedCharacterId = setup.LosingGuesses[index].Id }).ToList()
        };
        setup.GameStatsStore.Sessions[completedDaily.GameId] = completedDaily;

        var result = await setup.Manager.StartGameAsync(GameMode.Archive, 3);

        result.Should().Be(Extensions.LoadGameResult.LoadedExisting);
        setup.Manager.GameState.GameMode.Should().Be(GameMode.Archive);
        setup.Manager.GameState.IsLost.Should().BeTrue();
        setup.Manager.GameState.IsFinished.Should().BeTrue();
    }

    [Fact]
    public async Task ArchiveRemainsPlayableWhenDailyIsUnfinished()
    {
        var setup = CreateSetup();
        var unfinishedDaily = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = setup.PlayerStatsStore.Stats.Id,
            Mode = GameMode.Daily,
            DailyNumber = 3,
            TargetCharacterId = setup.Target.Id,
            StartedOnUtc = DateTimeOffset.UtcNow,
            Guesses = [new GameGuess { GameId = Guid.NewGuid(), GuessOrder = 0, GuessedCharacterId = setup.IncorrectGuess.Id }]
        };
        setup.GameStatsStore.Sessions[unfinishedDaily.GameId] = unfinishedDaily;

        var result = await setup.Manager.StartGameAsync(GameMode.Archive, 3);

        result.Should().Be(Extensions.LoadGameResult.CreatedNew);
        setup.Manager.GameState.GameMode.Should().Be(GameMode.Archive);
        setup.Manager.GameState.IsFinished.Should().BeFalse();
    }

    [Fact]
    public async Task ArchiveRemainsPlayableWithoutDailyHistory()
    {
        var setup = CreateSetup();

        var result = await setup.Manager.StartGameAsync(GameMode.Archive, 3);

        result.Should().Be(Extensions.LoadGameResult.CreatedNew);
        setup.Manager.GameState.GameMode.Should().Be(GameMode.Archive);
        setup.Manager.GameState.IsFinished.Should().BeFalse();
    }

    [Fact]
    public async Task ArchiveIgnoresOtherPlayersCompletedDaily()
    {
        var setup = CreateSetup();
        var otherPlayerSession = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Mode = GameMode.Daily,
            DailyNumber = 3,
            TargetCharacterId = setup.Target.Id,
            FinishedOnUtc = DateTimeOffset.UtcNow,
            IsWon = true,
        };
        setup.GameStatsStore.Sessions[otherPlayerSession.GameId] = otherPlayerSession;

        var result = await setup.Manager.StartGameAsync(GameMode.Archive, 3);

        result.Should().Be(Extensions.LoadGameResult.CreatedNew);
        setup.Manager.GameState.GameMode.Should().Be(GameMode.Archive);
        setup.Manager.GameState.IsFinished.Should().BeFalse();
    }

    [Fact]
    public async Task ArchiveKeepsExistingLocalStateWhenPresent()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Archive, 3);
        var firstGameId = setup.Manager.GameState.GameId;

        var result = await setup.Manager.StartGameAsync(GameMode.Archive, 3);

        result.Should().Be(Extensions.LoadGameResult.LoadedExisting);
        setup.Manager.GameState.GameId.Should().Be(firstGameId);
    }

    [Fact]
    public async Task ArchiveRejectsCachedStateWithDifferentDay()
    {
        var setup = CreateSetup();

        await setup.Manager.StartGameAsync(GameMode.Archive, 3);

        setup.GameStateStore.State = new GameState
        {
            GameId = Guid.NewGuid(),
            PlayerId = setup.PlayerStatsStore.Stats.Id,
            GameMode = GameMode.Archive,
            Game = new Game
            {
                DayNumber = 5,
                TargetId = setup.Target.Id,
                PortraitUrl = string.Empty
            }
        };

        var result = await setup.Manager.StartGameAsync(GameMode.Archive, 3);

        result.Should().Be(Extensions.LoadGameResult.CreatedNew);
        setup.Manager.GameState.Game.DayNumber.Should().Be(3);
    }

    [Fact]
    public async Task ArchiveDoesNotChangeDailyStreakWhenLoadingCompletedDaily()
    {
        var setup = CreateSetup();
        var completedDaily = new GameSession
        {
            GameId = Guid.NewGuid(),
            PlayerId = setup.PlayerStatsStore.Stats.Id,
            Mode = GameMode.Daily,
            DailyNumber = 3,
            TargetCharacterId = setup.Target.Id,
            FinishedOnUtc = DateTimeOffset.UtcNow,
            IsWon = true,
            Guesses = [new GameGuess { GameId = Guid.NewGuid(), GuessOrder = 0, GuessedCharacterId = setup.Target.Id }]
        };
        setup.GameStatsStore.Sessions[completedDaily.GameId] = completedDaily;

        var beforeStreak = setup.PlayerStatsStore.Stats.CurrentStreak;
        var beforeGamesPlayed = setup.PlayerStatsStore.Stats.GamesPlayed;

        await setup.Manager.StartGameAsync(GameMode.Archive, 3);

        setup.PlayerStatsStore.Stats.CurrentStreak.Should().Be(beforeStreak);
        setup.PlayerStatsStore.Stats.GamesPlayed.Should().Be(beforeGamesPlayed);
    }

    [Fact]
    public async Task CompletedGamesIncludeLossesInAverageGuessCount()
    {
        var setup = CreateSetup();
        await setup.Manager.StartGameAsync(GameMode.Daily);

        foreach (var guess in setup.LosingGuesses)
        {
            await setup.Manager.MakeGuessAsync(guess.Id);
        }

        var session = setup.GameStatsStore.Sessions[setup.Manager.GameState.GameId];
        session.FinishedOnUtc.Should().NotBeNull();
        session.Guesses.Should().HaveCount(6);
    }

    private static Setup CreateSetup()
    {
        var target = CreateCharacter(1, "Target", joinDay: 10, languages: 2, pronouns: ["Any"], affiliations: ["Guild"], species: ["Human"]);
        var incorrectGuess = CreateCharacter(2, "IncorrectGuess", joinDay: 20, languages: 4, pronouns: ["He/Him"], affiliations: ["Other"], species: ["Unknown"]);
        var lose4 = CreateCharacter(4, "Lose4", joinDay: 40, languages: 1, pronouns: ["They/Them"], affiliations: ["Other"], species: ["Human"]);
        var lose5 = CreateCharacter(5, "Lose5", joinDay: 50, languages: 2, pronouns: ["Any"], affiliations: ["Other"], species: ["Human"]);
        var lose6 = CreateCharacter(6, "Lose6", joinDay: 60, languages: 3, pronouns: ["He/Him"], affiliations: ["Other"], species: ["Human"]);
        var lose7 = CreateCharacter(7, "Lose7", joinDay: 70, languages: 4, pronouns: ["She/Her"], affiliations: ["Other"], species: ["Human"]);
        var lose8 = CreateCharacter(8, "Lose8", joinDay: 80, languages: 1, pronouns: ["They/Them"], affiliations: ["Other"], species: ["Human"]);
        var lose9 = CreateCharacter(9, "Lose9", joinDay: 90, languages: 2, pronouns: ["Any"], affiliations: ["Other"], species: ["Human"]);
        var characterStore = new InMemoryCharacterStore([target, incorrectGuess, lose4, lose5, lose6, lose7, lose8, lose9], target.Id, new Dictionary<int, int> { [3] = target.Id });
        var playerStatsStore = new InMemoryPlayerStatsStore { Stats = new PlayerStats { Id = Guid.Parse("11111111-1111-1111-1111-111111111111") } };
        var gameStateStore = new InMemoryGameStateStore();
        var gameStatsStore = new InMemoryGameStatsStore();
        var statisticsService = new StatisticsService(playerStatsStore, gameStatsStore);
        var manager = new GameStateManager(gameStateStore, new GameService(characterStore), playerStatsStore, characterStore, new CharacterComparer(characterStore), statisticsService);
        return new Setup(manager, gameStateStore, playerStatsStore, gameStatsStore, statisticsService, target, incorrectGuess, [incorrectGuess, lose4, lose5, lose6, lose7, lose8, lose9]);
    }

    private static Character CreateCharacter(int id, string name, int joinDay, int languages, string[] pronouns, string[] affiliations, string[] species) => new()
    {
        Id = id,
        Name = name,
        MinecraftUsername = name.ToLowerInvariant(),
        Aliases = [],
        IconUrl = $"https://example.com/{id}.png",
        Pronouns = pronouns.ToList(),
        Languages = languages,
        Affiliations = affiliations.ToList(),
        Species = species.ToList(),
        JoinDayNumber = joinDay,
    };

    private sealed record Setup(
        GameStateManager Manager,
        InMemoryGameStateStore GameStateStore,
        InMemoryPlayerStatsStore PlayerStatsStore,
        InMemoryGameStatsStore GameStatsStore,
        StatisticsService StatisticsService,
        Character Target,
        Character IncorrectGuess,
        IReadOnlyList<Character> LosingGuesses);
}
