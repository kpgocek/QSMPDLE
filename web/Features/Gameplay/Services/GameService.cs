using QSMPDLE.Web.Features.Gameplay.Helpers;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Infrastructure.Persistence;

namespace QSMPDLE.Web.Features.Gameplay.Services;

public sealed class GameService(ICharacterStore CharacterStore, IDayService DayService) : IGameService
{
    public async Task<GameState> StartDailyAsync(CancellationToken cancellationToken = default)
    {
        var game = await StartGameForDayAsync(DayService.GetTodayDayNumber(), cancellationToken);

        return CreateCanonicalState(game, EntryPoint.Daily);
    }

    public async Task<GameState> StartPracticeAsync(CancellationToken cancellationToken = default)
    {
        var today = DayService.GetArchiveDate(DayService.GetTodayDayNumber());
        var character = await CharacterStore.GetRandomCharacterAsync(today, cancellationToken);

        var game = new Game
        {
            TargetId = character.Id,
            PortraitUrl = CharacterUrlHelper.GetPortraitFolder(character.PortraitId)
        };

        return new GameState
        {
            Game = game,
            GameId = Guid.NewGuid(),
            SessionCategory = SessionCategory.Practice,
            EntryPoint = EntryPoint.Practice,
            FirstEntryPoint = EntryPoint.Practice,
        };
    }

    public async Task<GameState> StartArchivalAsync(int dayNumber, CancellationToken cancellationToken = default)
    {
        var game = await StartGameForDayAsync(dayNumber, cancellationToken);

        return CreateCanonicalState(game, EntryPoint.Archive);
    }

    private async Task<Game> StartGameForDayAsync(int dayNumber, CancellationToken cancellationToken = default)
    {
        var character = await CharacterStore.GetCharacterForDayAsync(dayNumber, cancellationToken);

        ArgumentNullException.ThrowIfNull(character, nameof(character));

        return new Game
        {
            TargetId = character.Id,
            PortraitUrl = CharacterUrlHelper.GetPortraitFolder(character.PortraitId),
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
    };
}
