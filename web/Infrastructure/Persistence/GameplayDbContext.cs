using Microsoft.EntityFrameworkCore;
using QSMPDLE.Web.Features.Gameplay.Models;

namespace QSMPDLE.Web.Infrastructure.Persistence;

public sealed class GameplayDbContext(DbContextOptions<GameplayDbContext> options) : DbContext(options)
{
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<DailyGame> DailyGames => Set<DailyGame>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DailyGame>()
            .HasIndex(x => x.Date)
            .IsUnique();

        base.OnModelCreating(modelBuilder);
    }
}
