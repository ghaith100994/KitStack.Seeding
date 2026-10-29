namespace KitStack.Seeding;

/// <summary>What the runner does when a seeder throws.</summary>
public enum SeedFailurePolicy
{
    /// <summary>Stop the run and throw a <see cref="SeedingException"/> (default).</summary>
    Throw = 0,

    /// <summary>Log the failure as critical, record it in the <see cref="SeedingResult"/> and run the next seeder.</summary>
    LogAndContinue = 1,
}
