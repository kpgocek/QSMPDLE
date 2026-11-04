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

        modelBuilder.Entity<GameGuess>()
            .HasIndex(x => x.GameId).IsUnique(false);

        modelBuilder.Entity<DailyGame>()
            .HasIndex(x => x.Date)
            .IsUnique();

        base.OnModelCreating(modelBuilder);
    }
}
