using Microsoft.EntityFrameworkCore;
using QSMPDLE.Web.Models;

namespace QSMPDLE.Web.Data;

public sealed class QsmpdleDbContext(DbContextOptions<QsmpdleDbContext> options) : DbContext(options)
{
    public DbSet<Character> Members => Set<Character>();
    public DbSet<DailyGame> DailyGames => Set<DailyGame>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DailyGame>()
            .HasIndex(x => x.Date)
            .IsUnique();

        base.OnModelCreating(modelBuilder);
    }
}
