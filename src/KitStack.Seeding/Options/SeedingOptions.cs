namespace KitStack.Seeding;

/// <summary>Seeding settings of one context, configured through <c>AddSeeding&lt;TContext&gt;(o =&gt; ...)</c>.</summary>
public sealed class SeedingOptions
{
    /// <summary>The folder used when no file source is configured: <c>Seeding/Files</c> under the application's base directory.</summary>
    public const string DefaultContentFolder = "Seeding/Files";

    /// <summary>Set to <see langword="false"/> to skip every seeder of the context (e.g. from configuration in an environment).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>What happens when a seeder throws. Defaults to <see cref="SeedFailurePolicy.Throw"/>.</summary>
    public SeedFailurePolicy FailurePolicy { get; set; } = SeedFailurePolicy.Throw;

    /// <summary>Names (<see cref="ISeeder{TContext}.Name"/>, case-insensitive) of seeders that must not run.</summary>
    public ISet<string> DisabledSeeders { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Where the context's seed files are read from, in lookup order. Empty means <see cref="DefaultContentFolder"/>.</summary>
    public SeedFileSourceCollection Files { get; } = new();

    /// <summary>The options name used for <typeparamref name="TContext"/> (its full type name).</summary>
    public static string NameFor<TContext>() => NameFor(typeof(TContext));

    /// <summary>The options name used for <paramref name="contextType"/> (its full type name).</summary>
    public static string NameFor(Type contextType)
    {
        ArgumentNullException.ThrowIfNull(contextType);
        return contextType.FullName ?? contextType.Name;
    }
}
