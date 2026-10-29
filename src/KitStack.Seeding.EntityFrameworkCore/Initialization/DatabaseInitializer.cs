using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KitStack.Seeding.EntityFrameworkCore;

/// <summary>
/// Prepares the database of <typeparamref name="TContext"/> (migrate or create) and then runs its seeders. Resolve it
/// from a scope, or call <c>services.InitializeDatabaseAsync&lt;TContext&gt;()</c>.
/// </summary>
/// <remarks>
/// Do not combine with <c>UseKitStackSeeding</c> on the same context: EF Core would then also run the seeders from
/// <c>Migrate</c>/<c>EnsureCreated</c>. Seeders are idempotent, so this is harmless but wasted work.
/// </remarks>
public class DatabaseInitializer<TContext>(TContext context, ISeederRunner<TContext> runner, ILogger<DatabaseInitializer<TContext>> logger)
    where TContext : DbContext
{
    /// <summary>Prepares the database as <paramref name="creation"/> says, then seeds.</summary>
    public virtual async Task<SeedingResult> InitializeAsync(DatabaseCreation creation = DatabaseCreation.Migrate, CancellationToken cancellationToken = default)
    {
        var database = context.Database;
        switch (creation)
        {
            case DatabaseCreation.Migrate when database.IsRelational():
                var pending = (await database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();
                if (pending.Count > 0)
                {
                    SeedLog.Migrating(logger, pending.Count, typeof(TContext).Name);
                    await database.MigrateAsync(cancellationToken).ConfigureAwait(false);
                }

                break;
            case DatabaseCreation.Migrate:
            case DatabaseCreation.EnsureCreated:
                await database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
                break;
            case DatabaseCreation.None:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(creation), creation, null);
        }

        return await runner.RunAsync(context, cancellationToken).ConfigureAwait(false);
    }
}
