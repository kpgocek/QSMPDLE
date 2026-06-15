using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QSMPDLE.Web.Data;
using QSMPDLE.Web.Data.Gameplay;
using QSMPDLE.Web.Models;

namespace QSMPDLE.Web.Services;

public sealed class GameService(QsmpdleDbContext Qsmp, IMemoryCache Cache) : IGameService
{
    public async Task<Game> StartDailyAsync(CancellationToken cancellationToken = default) => new() { Target = await GetDailyMemberAsync(cancellationToken), DayNumber = await GetDayAsync(cancellationToken) };

    private async Task<int> GetDayAsync(CancellationToken cancellationToken)
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

    private async Task<Member> GetDailyMemberAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return await Cache.GetOrCreateAsync($"daily-character-{today}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromDays(1);

            var member = await Qsmp.DailyGames
                    .Where(x => x.Date == today)
                    .Select(x => x.Member)
                    .SingleAsync(cancellationToken);

            if (member is not null) return member;

            var random = new Random(today.DayNumber);
            var index = random.Next(Qsmp.Members.Count());

            return Qsmp.Members.ToList()[index];


        }) ?? throw new InvalidOperationException();
    }

    public Game StartEndless() => new() { Target = GetRandomMember() };

    private Member GetRandomMember() => Qsmp.Members.ToList()[Random.Shared.Next(Qsmp.Members.Count())];

    public Guess SubmitGuess(Game game, int memberId)
    {
        var guessed = Qsmp.Members.Single(x => x.Id == memberId);

        var result = MemberComparer.Compare(game.Target, guessed);

        game.Guesses.Add(result);

        return result;
    }
}
