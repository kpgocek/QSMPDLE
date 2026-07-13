using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace QSMPDLE.Web.Infrastructure.Persistence;

public static class DatabaseMigrationExtensions
{
    public static async Task MigrateDatabasesAsync(this WebApplication app)
    {
        var dbContextFactory = app.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var db = await dbContextFactory.CreateDbContextAsync();

        Debug.WriteLine(db.Database.ProviderName);
        Debug.WriteLine(db.Database.GetConnectionString());

        await db.Database.MigrateAsync();
    }
}
