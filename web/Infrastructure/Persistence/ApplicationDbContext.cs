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
            entity.HasIndex(x => x.GameId).IsUnique(false);

            entity.ToTable(table => table.HasCheckConstraint(
                "CK_GameStats_PlayerId_NotEmpty",
                "\"PlayerId\" <> '00000000-0000-0000-0000-000000000000'::uuid"));
        });

        modelBuilder.Entity<GameGuess>()
            .HasIndex(x => x.GameId).IsUnique(false);

        modelBuilder.Entity<DailyGame>()
            .HasIndex(x => x.Date)
            .IsUnique();

        modelBuilder.Entity<GlobalStatsView>(view =>
        {
            view.HasNoKey();
            view.ToView("global_stats");

            view.Property(x => x.TotalGames)
                .HasColumnName("total_games");

            view.Property(x => x.TotalPlayers)
                .HasColumnName("total_players");

            view.Property(x => x.TotalWins)
                .HasColumnName("total_wins");
        });

        base.OnModelCreating(modelBuilder);
    }
}
