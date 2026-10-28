using ClarioShop.Settings.Contracts;
using ClarioShop.Settings.Domain;
using ClarioShop.Settings.Persistence;
using ClarioShop.Settings.Services;
using Npgsql;

namespace ClarioShop.Settings.Features.Store;

internal static class StoreSettingsSaving
{
    /// <summary>
    /// Saves the settings and drops the cached copies. A concurrent change, or a concurrent first save, is a
    /// conflict rather than a lost update.
    /// </summary>
    public static async Task<Result<StoreSettingsInfo>> SaveStoreSettingsAsync(
        this SettingsDbContext db, StoreSettings settings, SettingsCache cache, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception is DbUpdateConcurrencyException
            || exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return StoreSettingsErrors.Concurrency;
        }

        await cache.InvalidateAsync(cancellationToken);
        return settings.ToInfo();
    }
}
