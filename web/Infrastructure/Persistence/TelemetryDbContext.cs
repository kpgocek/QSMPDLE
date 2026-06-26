using Microsoft.EntityFrameworkCore;
using QSMPDLE.Web.Features.Statistics.Models;

namespace QSMPDLE.Web.Infrastructure.Persistence;

public sealed class TelemetryDbContext(DbContextOptions<TelemetryDbContext> options) : DbContext(options)
{
    public DbSet<GameSession> GameStats => Set<GameSession>();
    public DbSet<GameGuess> GameGuess => Set<GameGuess>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameSession>()
            .HasIndex(x => x.GameId).IsUnique(false);

        modelBuilder.Entity<GameGuess>()
            .HasIndex(x => x.GameId).IsUnique(false);

        base.OnModelCreating(modelBuilder);
    }
}
