namespace KitStack.Seeding;

/// <summary>What happened to one seeder during a run.</summary>
public enum SeederStatus
{
    /// <summary>The seeder ran without throwing (it may still have found nothing to add).</summary>
    Succeeded = 0,

    /// <summary>The seeder threw; see <see cref="SeederOutcome.Exception"/>.</summary>
    Failed = 1,

    /// <summary>The seeder is listed in <c>SeedingOptions.DisabledSeeders</c> and did not run.</summary>
    Disabled = 2,
}

/// <summary>The outcome of one seeder.</summary>
/// <param name="Name">The seeder's <see cref="ISeeder{TContext}.Name"/>.</param>
/// <param name="Order">The seeder's <see cref="ISeeder{TContext}.Order"/>.</param>
/// <param name="Status">What happened.</param>
/// <param name="Duration">How long the seeder ran.</param>
/// <param name="Exception">The failure, when <paramref name="Status"/> is <see cref="SeederStatus.Failed"/>.</param>
public sealed record SeederOutcome(string Name, int Order, SeederStatus Status, TimeSpan Duration, Exception? Exception = null);

/// <summary>The outcome of a seeding run, one entry per seeder in run order.</summary>
/// <param name="Seeders">The seeders' outcomes in run order.</param>
public sealed record SeedingResult(IReadOnlyList<SeederOutcome> Seeders)
{
    /// <summary>A run that did nothing (seeding disabled).</summary>
    public static SeedingResult Empty { get; } = new([]);

    /// <summary><see langword="true"/> when no seeder failed.</summary>
    public bool Succeeded => Seeders.All(x => x.Status != SeederStatus.Failed);

    /// <summary>The seeders that failed (only possible with <see cref="SeedFailurePolicy.LogAndContinue"/>).</summary>
    public IEnumerable<SeederOutcome> Failures => Seeders.Where(x => x.Status == SeederStatus.Failed);
}
