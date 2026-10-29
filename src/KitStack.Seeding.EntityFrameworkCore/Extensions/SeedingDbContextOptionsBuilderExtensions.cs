using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KitStack.Seeding.EntityFrameworkCore;

/// <summary>Bridges KitStack.Seeding into EF Core's own seeding hooks (<c>UseSeeding</c>/<c>UseAsyncSeeding</c>).</summary>
public static class SeedingDbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Runs the seeders of <typeparamref name="TContext"/> whenever EF Core seeds: from <c>Migrate</c>,
    /// <c>EnsureCreated</c> and <c>dotnet ef database update</c>.
    /// <code>
    /// services.AddDbContext&lt;AppDbContext&gt;((sp, o) =&gt; o.UseSqlServer(cs).UseKitStackSeeding&lt;AppDbContext&gt;(sp));
    /// </code>
    /// The seeders are resolved from a new scope of <paramref name="services"/>; the context being migrated is passed
    /// to them. The synchronous path (<c>Migrate()</c>, <c>EnsureCreated()</c>) blocks on the asynchronous seeders.
    /// </summary>
    public static DbContextOptionsBuilder UseKitStackSeeding<TContext>(this DbContextOptionsBuilder options, IServiceProvider services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(services);
        return options
            .UseSeeding((context, _) => RunAsync<TContext>(services, context, CancellationToken.None).GetAwaiter().GetResult())
            .UseAsyncSeeding((context, _, cancellationToken) => RunAsync<TContext>(services, context, cancellationToken));
    }

    private static async Task RunAsync<TContext>(IServiceProvider services, DbContext context, CancellationToken cancellationToken)
        where TContext : DbContext
    {
        var scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var runner = scope.ServiceProvider.GetRequiredService<ISeederRunner<TContext>>();
            await runner.RunAsync((TContext)context, cancellationToken).ConfigureAwait(false);
        }
    }
}
