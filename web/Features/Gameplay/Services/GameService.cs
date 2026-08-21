using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Infrastructure.Persistence;

namespace QSMPDLE.Web.Features.Gameplay.Services;

public sealed class GameService(ICharacterStore CharacterStore, IDayService DayService) : IGameService
{
    public async Task<GameState> StartDailyAsync(CancellationToken cancellationToken = default)
    {
        var dayNumber = await GetTodayDayNumberAsync(cancellationToken);

        var game = await StartGameForDayAsync(dayNumber, cancellationToken);

        return game is null
            ? throw new InvalidOperationException($"Cannot initialize Archival game for day #{dayNumber}.")
            : CreateCanonicalState(game, EntryPoint.Daily);
    }

    public async Task<GameState> StartPracticeAsync(CancellationToken cancellationToken = default)
    {
        var character = await CharacterStore.GetRandomCharacterAsync(cancellationToken);

        var game = new Game
        {
            TargetId = character.Id,
            PortraitUrl = character.IconUrl
        };

        return new GameState
        {
            Game = game,
            GameId = Guid.NewGuid(),
            SessionCategory = SessionCategory.Practice,
            EntryPoint = EntryPoint.Practice,
            FirstEntryPoint = EntryPoint.Practice,
            GameMode = GameMode.Practice
        };
    }

    public async Task<GameState> StartArchivalAsync(int dayNumber, CancellationToken cancellationToken = default)
    {
        var game = await StartGameForDayAsync(dayNumber, cancellationToken);

        return game is null
            ? throw new InvalidOperationException($"Cannot initialize Archival game for day #{dayNumber}.")
            : CreateCanonicalState(game, EntryPoint.Archive);
    }

    private async Task<Game> StartGameForDayAsync(int dayNumber, CancellationToken cancellationToken = default)
    {
        var character = await CharacterStore.GetCharacterForDayAsync(dayNumber, cancellationToken);

        ArgumentNullException.ThrowIfNull(character, nameof(character));

        return new Game
        {
            TargetId = character.Id,
            PortraitUrl = character.IconUrl,
            PuzzleId = dayNumber
        };
    }

    private static GameState CreateCanonicalState(Game game, EntryPoint entryPoint) => new()
    {
        Game = game,
        GameId = Guid.NewGuid(),
        SessionCategory = SessionCategory.CanonicalPuzzle,
        EntryPoint = entryPoint,
        FirstEntryPoint = entryPoint,
        GameMode = entryPoint == EntryPoint.Daily ? GameMode.Daily : GameMode.Archive
    };

    public async Task<int> GetTodayDayNumberAsync(CancellationToken cancellationToken)
    {
        return DayService.GetTodayDayNumber();
    }

    public async Task<int> GetMaxArchiveDayAsync(CancellationToken cancellationToken)
    {
        return DayService.GetMaxArchiveDay();
    }

    public DateOnly GetFirstDay() => DayService.GetFirstDay();

}
