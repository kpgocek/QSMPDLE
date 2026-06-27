using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Infrastructure.Persistence;

namespace QSMPDLE.Web.Features.Gameplay.Services;

public sealed class GameService(ICharacterStore CharacterStore) : IGameService
{
    private static readonly DateOnly FirstDay = new(2026, 6, 15);

    public async Task<GameState> StartDailyAsync(CancellationToken cancellationToken = default)
    {
        var dayNumber = await GetTodayDayNumberAsync(cancellationToken);

        var game = await StartGameForDayAsync(dayNumber, cancellationToken);

        return game is null
            ? throw new InvalidOperationException($"Cannot initialize Archival game for day #{dayNumber}.")
            : new GameState { Game = game, GameId = Guid.NewGuid(), GameMode = GameMode.Daily };
    }

    public async Task<GameState> StartPracticeAsync(CancellationToken cancellationToken = default)
    {
        var character = await CharacterStore.GetRandomCharacterAsync(cancellationToken);

        var game = new Game
        {
            TargetId = character.Id,
            PortraitUrl = character.IconUrl
        };

        return new GameState { Game = game, GameId = Guid.NewGuid(), GameMode = GameMode.Practice };
    }

    public async Task<GameState> StartArchivalAsync(int dayNumber, CancellationToken cancellationToken = default)
    {
        var game = await StartGameForDayAsync(dayNumber, cancellationToken);

        return game is null
            ? throw new InvalidOperationException($"Cannot initialize Archival game for day #{dayNumber}.")
            : new GameState { Game = game, GameId = Guid.NewGuid(), GameMode = GameMode.Archive };
    }

    private async Task<Game> StartGameForDayAsync(int dayNumber, CancellationToken cancellationToken = default)
    {
        var character = await CharacterStore.GetCharacterForDayAsync(dayNumber, cancellationToken);

        ArgumentNullException.ThrowIfNull(character, nameof(character));

        return new Game
        {
            TargetId = character.Id,
            PortraitUrl = character.IconUrl,
            DayNumber = dayNumber
        };
    }

    public async Task<int> GetTodayDayNumberAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return today.DayNumber - FirstDay.DayNumber + 1;
    }




}
