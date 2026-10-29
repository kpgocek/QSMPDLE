using Microsoft.EntityFrameworkCore;

namespace QSMPDLE.Web.Infrastructure.Persistence;

public static class DatabaseMigrationExtensions
{
    public static async Task MigrateDatabasesAsync(this WebApplication app)
    {
        var dbContextFactory = app.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var db = await dbContextFactory.CreateDbContextAsync();

        await db.Database.MigrateAsync();
    }
}
