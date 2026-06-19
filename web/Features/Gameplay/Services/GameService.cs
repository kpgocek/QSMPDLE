using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QSMPDLE.Web.Data;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Models;

namespace QSMPDLE.Web.Features.Gameplay.Services;

public sealed class GameService(QsmpdleDbContext Qsmp, IMemoryCache Cache) : IGameService
{
    public async Task<Game> StartDailyAsync(CancellationToken cancellationToken = default) => new() { Target = await GetDailyCharacterAsync(cancellationToken), DayNumber = await GetDayAsync(cancellationToken) };

    public async Task<int> GetDayAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return await Cache.GetOrCreateAsync($"daily-number-{today}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromDays(1);

            var dayNo = await Qsmp.DailyGames
                    .Where(x => x.Date == today)
                    .Select(x => x.Id).SingleAsync(cancellationToken);

            return dayNo;
        });
    }

    public async Task<Character> GetDailyCharacterAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return await Cache.GetOrCreateAsync($"daily-character-{today}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromDays(1);

            var character = await Qsmp.DailyGames
                    .Where(x => x.Date == today)
                    .Select(x => x.Character)
                    .SingleAsync(cancellationToken);

            if (character is not null) return character;

            var random = new Random(today.DayNumber);
            var index = random.Next(Qsmp.Characters.Count());

            return Qsmp.Characters.ToList()[index];


        }) ?? throw new InvalidOperationException();
    }

    public async Task<Game> StartEndlessAsync() => new() { Target = await GetRandomCharacterAsync() };


    private async Task<Character> GetRandomCharacterAsync() => Qsmp.Characters.ToList().ElementAt(Random.Shared.Next(Qsmp.Characters.Count()));

    public Guess SubmitGuess(Game game, int characterId)
    {
        var guessed = Qsmp.Characters.Single(x => x.Id == characterId);

        var result = CharacterComparer.Compare(game.Target, guessed);

        game.Guesses.Add(result);

        return result;
    }
}
