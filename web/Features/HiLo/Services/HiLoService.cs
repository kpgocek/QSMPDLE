using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Gameplay.Services;
using QSMPDLE.Web.Features.HiLo.Models;
using QSMPDLE.Web.Infrastructure.LocalStorage;
using QSMPDLE.Web.Infrastructure.Persistence;

namespace QSMPDLE.Web.Features.HiLo.Services;

public sealed class HiLoService(
    ICharacterStore characterStore,
    IGameplayAvailabilityService availability,
    IPlayerStatsStore playerStatsStore,
    IHiLoScoreStore scoreStore) : IHiLoService
{
    public async Task<HiLoRound> CreateRoundAsync(HiLoRound? previous = null, CancellationToken cancellationToken = default)
    {
        var today = availability.GetEffectiveDate(null);
        var hasQPhones = availability.GetActiveClues(today).Contains(ClueGroup.QPhone);
        var characters = (await characterStore.GetCharactersAsync(cancellationToken)).Where(character => character.IsAvailableOn(today)).ToList();

        // Flat on purpose: a stat comes up as often as characters have it, so a rare one does not repeat its few pairs.
        var entries = Enum.GetValues<HiLoStat>()
            .Where(stat => hasQPhones || stat != HiLoStat.QPhoneNumber)
            .Select(stat => characters.Where(character => stat.GetValue(character) is not null).Select(character => (Stat: stat, Character: character)).ToList())
            .Where(statEntries => statEntries.Count > 1)
            .SelectMany(statEntries => statEntries)
            .ToList();

        if (previous is not null)
        {
            var carried = previous.Hidden;
            var drafts = entries
                .Where(entry => entry.Character.Id != carried.Id && entry.Character.Id != previous.Known.Id && entry.Stat.GetValue(carried) is not null)
                .ToList();

            // Nobody left to compare the carried character with starts a fresh pair instead of ending the run.
            if (drafts.Count > 0)
            {
                var draft = drafts[Random.Shared.Next(drafts.Count)];
                return new HiLoRound(draft.Stat, carried, draft.Stat.GetValue(carried)!.Value, draft.Character, draft.Stat.GetValue(draft.Character)!.Value);
            }
        }

        var (stat, known) = entries[Random.Shared.Next(entries.Count)];
        var others = entries.Where(entry => entry.Stat == stat && entry.Character.Id != known.Id).ToList();
        var hidden = others[Random.Shared.Next(others.Count)].Character;

        return new HiLoRound(stat, known, stat.GetValue(known)!.Value, hidden, stat.GetValue(hidden)!.Value);
    }

    public async Task<int> GetBestScoreAsync(CancellationToken cancellationToken = default) =>
        await scoreStore.GetBestScoreAsync(await playerStatsStore.GetPlayerIdAsync(), cancellationToken);

    public async Task RecordScoreAsync(int score, CancellationToken cancellationToken = default) =>
        await scoreStore.RecordScoreAsync(await playerStatsStore.GetPlayerIdAsync(), score, cancellationToken);
}
