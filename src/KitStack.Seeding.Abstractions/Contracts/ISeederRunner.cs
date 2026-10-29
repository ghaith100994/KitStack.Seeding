namespace KitStack.Seeding;

/// <summary>Runs every registered <see cref="ISeeder{TContext}"/> of a context in order.</summary>
/// <typeparam name="TContext">The context the seeders write to.</typeparam>
public interface ISeederRunner<in TContext>
    where TContext : class
{
    /// <summary>Runs the seeders against <paramref name="context"/>.</summary>
    /// <exception cref="SeedingException">A seeder failed and the failure policy is <see cref="SeedFailurePolicy.Throw"/>.</exception>
    Task<SeedingResult> RunAsync(TContext context, CancellationToken cancellationToken = default);
}
