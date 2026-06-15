using System.Collections.Concurrent;
using WikiScraperQSMP.Data;
using Microsoft.EntityFrameworkCore;
using WikiScraperQSMP.Models;
using WikiScraperQSMP.Services;

var databasePath = Path.Combine(Directory.GetCurrentDirectory(), "../../db/qsmpdle.db");

var dbOptions = new DbContextOptionsBuilder<QsmpdleDbContext>()
    .UseSqlite($"Data Source={databasePath}")
    .Options;

var httpClient = new HttpClient();
var apiClient = new WikiApiClient(httpClient);

var memberListScraper = new MemberListScraper(apiClient);
var memberPageScraper = new MemberPageScraper(apiClient);

var usernames = await memberListScraper.GetMembers();
var members = new ConcurrentBag<Member>();

await Parallel.ForEachAsync(
    usernames,
    new ParallelOptions
    {
        MaxDegreeOfParallelism = 5
    },
    async (username, ct) =>
    {
        try
        {
            Console.WriteLine($"Scraping {username}");

            var member =
                await memberPageScraper.Scrape(username);

            if (member is not null)
            {
                members.Add(member);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED: {username}");
            Console.WriteLine(ex.Message);
        }
    });

var result = members.ToList();

await using var db = new QsmpdleDbContext(dbOptions);
await db.Database.EnsureCreatedAsync();

foreach (var member in result)
{
    var existing = await db.Members.SingleOrDefaultAsync(x => x.Name == member.Name);

    if (existing is null)
    {
        db.Members.Add(member);
        continue;
    }

    existing.AliasesJson = member.AliasesJson;
    existing.PronounsJson = member.PronounsJson;
    existing.Languages = member.Languages;
    existing.AffiliationsJson = member.AffiliationsJson;
    existing.SpeciesJson = member.SpeciesJson;
    existing.CharacterIconUrl = member.CharacterIconUrl;
    existing.JoinDayNumber = member.JoinDayNumber;
    existing.MemberPageUrl = member.MemberPageUrl;
    existing.MinecraftUsername = member.MinecraftUsername;
}

await db.SaveChangesAsync();

Console.WriteLine($"Saved {result.Count} members to {databasePath}");
