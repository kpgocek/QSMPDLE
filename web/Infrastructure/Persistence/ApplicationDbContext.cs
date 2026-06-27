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
        modelBuilder.Entity<GameSession>()
            .HasIndex(x => x.GameId).IsUnique(false);

        modelBuilder.Entity<GameGuess>()
            .HasIndex(x => x.GameId).IsUnique(false);

        modelBuilder.Entity<DailyGame>()
            .HasIndex(x => x.Date)
            .IsUnique();

        base.OnModelCreating(modelBuilder);
    }
}
