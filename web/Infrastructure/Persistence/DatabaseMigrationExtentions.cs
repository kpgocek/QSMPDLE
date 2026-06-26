using Microsoft.EntityFrameworkCore;

namespace QSMPDLE.Web.Infrastructure.Persistence;

public static class DatabaseMigrationExtensions
{
    public static async Task MigrateDatabasesAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        await scope.ServiceProvider
            .GetRequiredService<GameplayDbContext>()
            .Database
            .MigrateAsync();

        await scope.ServiceProvider
            .GetRequiredService<TelemetryDbContext>()
            .Database
            .MigrateAsync();
    }
}