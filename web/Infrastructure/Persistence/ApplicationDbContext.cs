using Microsoft.EntityFrameworkCore;
using QSMPDLE.Web.Features.Gameplay.Models;
using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<DailyGame> DailyGames => Set<DailyGame>();
    public DbSet<GameSession> GameStats => Set<GameSession>();
    public DbSet<GameGuess> GameGuess => Set<GameGuess>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameSession>(entity =>
        {
            entity.HasIndex(x => x.GameId).IsUnique();
            entity.HasIndex(x => new { x.PlayerId, x.PuzzleId })
                .HasFilter("\"PuzzleId\" IS NOT NULL AND \"SessionCategory\" = 0 AND NOT \"IsLegacyDuplicate\"")
                .IsUnique();
            entity.HasIndex(x => x.PlayerId)
                .HasDatabaseName("IX_GameStats_PlayerId")
                .HasFilter("NOT \"IsLegacyDuplicate\"");

            entity.ToTable(table => table.HasCheckConstraint(
                "CK_GameStats_PlayerId_NotEmpty",
                "\"PlayerId\" <> '00000000-0000-0000-0000-000000000000'::uuid"));
        });

        modelBuilder.Entity<GameGuess>(entity =>
        {
            entity.HasIndex(x => x.GameId).IsUnique(false);

            // Explicit because EF takes the filtered indexes below for a replacement, which they are not for older rows.
            entity.HasIndex("GameSessionId");

            // Only guesses recorded with a time: older rows may hold duplicates that must not fail the migration.
            entity.HasIndex("GameSessionId", nameof(Features.Statistics.Models.GameGuess.GuessOrder))
                .HasFilter("\"GuessedOnUtc\" IS NOT NULL")
                .IsUnique();
            entity.HasIndex("GameSessionId", nameof(Features.Statistics.Models.GameGuess.GuessedCharacterId))
                .HasFilter("\"GuessedOnUtc\" IS NOT NULL")
                .IsUnique();
        });

        modelBuilder.Entity<DailyGame>()
            .HasIndex(x => x.Date)
            .IsUnique();

        base.OnModelCreating(modelBuilder);
    }
}
