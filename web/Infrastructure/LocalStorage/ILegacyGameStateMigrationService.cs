namespace QSMPDLE.Web.Infrastructure.LocalStorage;

/// <summary>Migrates all browser-only Daily and Archive states to canonical puzzle keys.</summary>
public interface ILegacyGameStateMigrationService
{
    Task MigrateAsync();
}
