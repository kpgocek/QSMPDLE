using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Statistics.Models;
using QSMPDLE.Web.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace QSMPDLE.Web.Tests.Infrastructure;

public sealed class DatabaseGameStatsStoreTests : IAsyncLifetime
{
    private const int Target = 100;
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18").Build();
    private DbContextOptions<ApplicationDbContext> options = null!;
    private DatabaseGameStatsStore store = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;

        await using var database = new ApplicationDbContext(options);
        await database.Database.MigrateAsync();
        store = new DatabaseGameStatsStore(new TestDbContextFactory(options));
    }

    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    [Theory]
    [InlineData(SessionCategory.CanonicalPuzzle)]
    [InlineData(SessionCategory.Practice)]
    public async Task RecordGuessAsync_WholeGame_StoresOneSessionWithItsGuessesAndOutcome(SessionCategory category)
    {
        var game = CreateGame(category);

        await store.RecordGuessAsync(Copy(game), [1], Now);
        await store.RecordGuessAsync(Copy(game, Now.AddHours(1), Ruleset.V2), [1, Target], Now.AddMinutes(1));

        var session = await LoadSingleAsync();
        session.GameId.Should().Be(game.GameId);
        session.StartedOnUtc.Should().Be(Now, "later guesses must not move the start");
        session.Ruleset.Should().Be(Ruleset.Classic, "the first guess fixes the ruleset");
        session.GuessedCharacterIds.Should().Equal(1, Target);
        session.Guesses.Select(guess => guess.GuessedOnUtc).Should().BeEquivalentTo([Now, Now.AddMinutes(1)]);
        session.FinishedOnUtc.Should().Be(Now.AddMinutes(1));
        session.IsWon.Should().BeTrue();
    }

    [Fact]
    public async Task RecordGuessAsync_DeliveredTwice_CountsOnce()
    {
        var game = CreateGame();

        await store.RecordGuessAsync(Copy(game), [1], Now);
        var stored = await store.RecordGuessAsync(Copy(game), [1], Now.AddSeconds(5));

        stored.GuessedCharacterIds.Should().Equal(1);
        (await LoadSingleAsync()).Guesses.Should().ContainSingle();
    }

    [Theory]
    [InlineData(SessionCategory.CanonicalPuzzle)]
    [InlineData(SessionCategory.Practice)]
    public async Task RecordGuessAsync_TabsGuessAtOnce_OneBoardWins(SessionCategory category)
    {
        var game = CreateGame(category);

        var results = await Task.WhenAll(Enumerable.Range(1, 8)
            .Select(characterId => Task.Run(() => store.RecordGuessAsync(Copy(game), [characterId], Now))));

        var session = await LoadSingleAsync();
        session.Guesses.Should().ContainSingle();
        results.Should().OnlyContain(result => result.GuessedCharacterIds.SequenceEqual(session.GuessedCharacterIds));
    }

    [Fact]
    public async Task RecordGuessAsync_TabsContinueAtOnce_OnlyOneSecondGuessIsKept()
    {
        var game = CreateGame();
        await store.RecordGuessAsync(Copy(game), [1], Now);

        await Task.WhenAll(Enumerable.Range(2, 8).Select(second => Task.Run(() =>
            store.RecordGuessAsync(Copy(game), [1, second], Now))));

        var session = await LoadSingleAsync();
        session.Guesses.Should().HaveCount(2);
        session.Guesses.Select(guess => guess.GuessOrder).Should().BeEquivalentTo([0, 1]);
    }

    [Fact]
    public async Task RecordGuessAsync_PuzzleClaimedUnderAnotherGameId_ReturnsThatGameUnchanged()
    {
        var first = CreateGame();
        await store.RecordGuessAsync(Copy(first), [1], Now);

        var stored = await store.RecordGuessAsync(CreateGame(playerId: first.PlayerId), [1, 2], Now);

        stored.GameId.Should().Be(first.GameId);
        (await LoadSingleAsync()).GuessedCharacterIds.Should().Equal(1);
    }

    [Fact]
    public async Task RecordGuessAsync_TabsWithDifferentGameIdsStartAtOnce_ShareOneCanonicalSession()
    {
        var playerId = Guid.NewGuid();

        var results = await Task.WhenAll(Enumerable.Range(1, 8)
            .Select(characterId =>
                Task.Run(() => store.RecordGuessAsync(CreateGame(playerId: playerId), [characterId], Now))));

        var session = await LoadSingleAsync();
        session.Guesses.Should().ContainSingle();
        results.Should().OnlyContain(result => result.GameId == session.GameId);
    }

    [Fact]
    public async Task RecordGuessAsync_PracticeGames_AreIndependentSessions()
    {
        var playerId = Guid.NewGuid();

        await store.RecordGuessAsync(CreateGame(SessionCategory.Practice, playerId), [1], Now);
        await store.RecordGuessAsync(CreateGame(SessionCategory.Practice, playerId), [1], Now);
        await store.RecordGuessAsync(CreateGame(playerId: playerId), [1], Now);

        await using var database = new ApplicationDbContext(options);
        (await database.GameStats.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task GetPlayerGames_OneCategory_LeavesTheOtherInTheDatabase()
    {
        var player = Guid.NewGuid();
        await using (var database = new ApplicationDbContext(options))
        {
            database.GameStats.AddRange(
                new GameSession
                {
                    GameId = Guid.NewGuid(), PlayerId = player, SessionCategory = SessionCategory.CanonicalPuzzle
                },
                new GameSession
                {
                    GameId = Guid.NewGuid(), PlayerId = player, SessionCategory = SessionCategory.Practice
                });
            await database.SaveChangesAsync();
        }

        (await store.GetPlayerGames(player, SessionCategory.CanonicalPuzzle)).Should().ContainSingle().Which
            .SessionCategory.Should().Be(SessionCategory.CanonicalPuzzle);
        (await store.GetPlayerGames(player)).Should().HaveCount(2);
    }

    [Fact]
    public async Task RecordGuessAsync_LegacyDuplicateOfThePuzzle_IsNeverContinued()
    {
        var duplicate = CreateGame();
        duplicate.IsLegacyDuplicate = true;
        await AddAsync(duplicate);

        var game = CreateGame(playerId: duplicate.PlayerId);
        var stored = await store.RecordGuessAsync(game, [1], Now);

        stored.GameId.Should().Be(game.GameId);
        (await store.GetPlayerCanonicalGamesByPuzzleRangeAsync(duplicate.PlayerId, 1, 10))
            .Should().ContainSingle().Which.GameId.Should().Be(game.GameId);
    }

    [Fact]
    public async Task RecordGuessAsync_SessionFromBeforeGuessTimes_IsContinued()
    {
        var legacy = CreateGame();
        legacy.Ruleset = Ruleset.Unknown;
        // Orders recorded by older versions can repeat.
        legacy.Guesses = [LegacyGuess(legacy, 1), LegacyGuess(legacy, 2)];
        await AddAsync(legacy);

        var stored = await store.RecordGuessAsync(CreateGame(gameId: legacy.GameId, playerId: legacy.PlayerId),
            [.. legacy.GuessedCharacterIds, 3], Now);

        stored.Guesses.Should().HaveCount(3);
        stored.GuessedCharacterIds[^1].Should().Be(3);
        stored.Ruleset.Should().Be(Ruleset.Unknown);
    }

    private static GameGuess LegacyGuess(GameSession session, int characterId) =>
        new() { GameId = session.GameId, GuessOrder = 0, GuessedCharacterId = characterId };

    private async Task AddAsync(GameSession session)
    {
        await using var database = new ApplicationDbContext(options);
        database.GameStats.Add(session);
        await database.SaveChangesAsync();
    }

    private async Task<GameSession> LoadSingleAsync()
    {
        await using var database = new ApplicationDbContext(options);
        return await database.GameStats.AsNoTracking().Include(session => session.Guesses).SingleAsync();
    }

    private static GameSession
        CreateGame(SessionCategory category = SessionCategory.CanonicalPuzzle, Guid? playerId = null,
            Guid? gameId = null) => new()
    {
        GameId = gameId ?? Guid.NewGuid(),
        PlayerId = playerId ?? Guid.NewGuid(),
        PuzzleId = category == SessionCategory.CanonicalPuzzle ? 5 : null,
        SessionCategory = category,
        TargetCharacterId = Target,
        Ruleset = Ruleset.Classic,
        StartedOnUtc = Now
    };

    // Every call brings its own instance, as every tab and every guess does.
    private static GameSession Copy(GameSession game, DateTimeOffset? startedOnUtc = null, Ruleset? ruleset = null) =>
        new()
        {
            GameId = game.GameId,
            PlayerId = game.PlayerId,
            PuzzleId = game.PuzzleId,
            SessionCategory = game.SessionCategory,
            TargetCharacterId = game.TargetCharacterId,
            Ruleset = ruleset ?? game.Ruleset,
            StartedOnUtc = startedOnUtc ?? game.StartedOnUtc
        };

    private sealed class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);
    }
}
