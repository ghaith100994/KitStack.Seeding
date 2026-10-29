namespace KitStack.Seeding;

/// <summary>
/// Seeds data into <typeparamref name="TContext"/> (usually an EF Core <c>DbContext</c>). Seeders of a context run once
/// per <see cref="ISeederRunner{TContext}.RunAsync"/> call, ordered by <see cref="Order"/> and then by <see cref="Name"/>.
/// </summary>
/// <remarks>
/// A seeder must be idempotent: it runs at every start, so it checks what already exists before it writes.
/// </remarks>
/// <typeparam name="TContext">The context the seeder writes to.</typeparam>
public interface ISeeder<in TContext>
    where TContext : class
{
    /// <summary>Position in the run; lower values run first. Keep the values in one constants class per context.</summary>
    int Order { get; }

    /// <summary>Name used in logs, results and <c>SeedingOptions.DisabledSeeders</c>. Defaults to the type name.</summary>
    string Name => GetType().Name;

    /// <summary>Seeds the data. Throw to report a failure; the runner applies the configured <see cref="SeedFailurePolicy"/>.</summary>
    Task SeedAsync(TContext context, CancellationToken cancellationToken);
}
