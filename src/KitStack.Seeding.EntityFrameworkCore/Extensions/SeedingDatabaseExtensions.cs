using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KitStack.Seeding.EntityFrameworkCore;

/// <summary>Startup helpers that prepare a database and seed it.</summary>
public static class SeedingDatabaseExtensions
{
    /// <summary>
    /// In a new scope: migrates (or creates) the database of <typeparamref name="TContext"/>, then runs its seeders.
    /// <code>await app.Services.InitializeDatabaseAsync&lt;AppDbContext&gt;();</code>
    /// </summary>
    public static async Task<SeedingResult> InitializeDatabaseAsync<TContext>(
        this IServiceProvider services, DatabaseCreation creation = DatabaseCreation.Migrate, CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        var scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var initializer = scope.ServiceProvider.GetService<DatabaseInitializer<TContext>>()
                ?? ActivatorUtilities.CreateInstance<DatabaseInitializer<TContext>>(scope.ServiceProvider);
            return await initializer.InitializeAsync(creation, cancellationToken).ConfigureAwait(false);
        }
    }
}
