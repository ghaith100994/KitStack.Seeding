using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KitStack.Seeding;

/// <summary>Registration and run helpers for KitStack.Seeding.</summary>
public static class SeedingServiceCollectionExtensions
{
    /// <summary>
    /// Adds seeding for <typeparamref name="TContext"/>: the <see cref="ISeederRunner{TContext}"/>, the context's
    /// <see cref="ISeedFileSource{TContext}"/> and its <see cref="SeedingOptions"/>. Call it once per context; calling
    /// it again only adds <paramref name="configure"/>. Logging must be registered (<c>AddLogging()</c>; hosts do this).
    /// <code>
    /// services.AddSeeding&lt;AppDbContext&gt;(o =&gt; o.Files.FromContentFolder("Seeding/Files"))
    ///         .AddSeedersFromAssemblyContaining&lt;AppDbContext&gt;();
    /// </code>
    /// </summary>
    public static SeedingBuilder<TContext> AddSeeding<TContext>(this IServiceCollection services, Action<SeedingOptions>? configure = null)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.AddOptions<SeedingOptions>(SeedingOptions.NameFor<TContext>());
        if (configure is not null)
            options.Configure(configure);

        services.TryAddSingleton<ISeedFileSource<TContext>, ConfiguredSeedFileSource<TContext>>();
        services.TryAddTransient<ISeederRunner<TContext>, SeederRunner<TContext>>();
        return new SeedingBuilder<TContext>(services);
    }

    /// <summary>
    /// Runs the seeders of <typeparamref name="TContext"/> in a new scope, with the context resolved from that scope.
    /// For EF Core use <c>InitializeDatabaseAsync&lt;TContext&gt;()</c>, which can also migrate first.
    /// </summary>
    public static async Task<SeedingResult> RunSeedersAsync<TContext>(this IServiceProvider services, CancellationToken cancellationToken = default)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(services);
        var scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            var runner = scope.ServiceProvider.GetRequiredService<ISeederRunner<TContext>>();
            return await runner.RunAsync(context, cancellationToken).ConfigureAwait(false);
        }
    }
}
