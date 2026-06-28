using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace QSMPDLE.Web.Infrastructure.Persistence;

public static class DatabaseMigrationExtensions
{
    public static async Task MigrateDatabasesAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Debug.WriteLine(db.Database.ProviderName);
        Debug.WriteLine(db.Database.GetConnectionString());

        await db.Database.MigrateAsync();
    }
}