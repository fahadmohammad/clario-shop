using ClarioShop.Inventory.Domain;
using Npgsql;

namespace ClarioShop.Inventory.Services;

/// <summary>
/// Runs a load-change-save operation and retries it when another request changed the same stock rows
/// first (RowVersion mismatch or, in a repeatable-read transaction, a serialization failure) or created the same
/// row (unique violation). The operation must do all its reads inside the delegate, because the change tracker is
/// cleared before each retry.
/// </summary>
internal static class ConcurrencyRetry
{
    public const int MaxAttempts = 8;

    public static async Task<Result<T>> ExecuteAsync<T>(
        DbContext db,
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var result = await operation(cancellationToken);
                if (result.IsFailure)
                {
                    // Drop half-applied changes so a later SaveChanges in this scope cannot persist them.
                    db.ChangeTracker.Clear();
                }

                return result;
            }
            catch (DbUpdateException exception) when (IsConflict(exception))
            {
                db.ChangeTracker.Clear();
                if (attempt == MaxAttempts)
                {
                    return InventoryErrors.Concurrency;
                }

                await Task.Delay(Backoff(attempt), cancellationToken);
            }
        }
    }

    private static bool IsConflict(DbUpdateException exception) =>
        exception is DbUpdateConcurrencyException
        || exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure };

    private static TimeSpan Backoff(int attempt) =>
        TimeSpan.FromMilliseconds(Random.Shared.Next(5, 20) * attempt);
}
