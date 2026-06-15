var memberIds = new[]
{
    1,2,3,4,5,6,7,8,9,10,
    11,12,13,16,17,18,19,20,
    21,22,23,24,25,26,27,28,29,30,
    31,32,33,34,35,36,37,38,39,40,
    41,42,43,44,45,46,47,48,49,50,
    51,52,53,54,55,56,57,58,59,60
};

var rng = new Random(42); // deterministic

var recent = new Queue<int>();
var appearances = memberIds.ToDictionary(x => x, _ => 0);

Console.WriteLine("BEGIN TRANSACTION;");

for (var date = new DateOnly(2026, 6, 15);
     date <= new DateOnly(2026, 12, 31);
     date = date.AddDays(1))
{
    var available = memberIds
        .Where(id => !recent.Contains(id))
        .ToList();

    var minCount = available.Min(id => appearances[id]);

    var candidates = available
        .Where(id => appearances[id] == minCount)
        .ToList();

    var selected = candidates[rng.Next(candidates.Count)];

    appearances[selected]++;

    recent.Enqueue(selected);

    while (recent.Count > 10)
        recent.Dequeue();

    Console.WriteLine(
        $"INSERT INTO DailyGames (Date, MemberId) VALUES ('{date:yyyy-MM-dd}', {selected});");
}

Console.WriteLine("COMMIT;");
