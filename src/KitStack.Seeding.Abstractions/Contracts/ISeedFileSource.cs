namespace KitStack.Seeding;

/// <summary>
/// Where seed files are read from: a folder next to the application, embedded resources, memory, ... A seeder names
/// its file (<c>"01Countries.json"</c>) and never builds a path itself.
/// </summary>
public interface ISeedFileSource
{
    /// <summary>A human-readable description used in error messages, e.g. <c>folder '/app/Seeding/Files'</c>.</summary>
    string Description { get; }

    /// <summary>Opens <paramref name="fileName"/> for reading, or returns <see langword="null"/> when this source does not have it.</summary>
    Task<Stream?> TryOpenReadAsync(string fileName, CancellationToken cancellationToken);
}

/// <summary>
/// The seed files of one context, as configured by <c>AddSeeding&lt;TContext&gt;(o =&gt; o.Files...)</c>. Seeders take
/// this in their constructor, so two contexts of one application can read from different places.
/// </summary>
/// <typeparam name="TContext">The context whose seed files this is.</typeparam>
#pragma warning disable CA1040 // Marker interface: it selects the per-context registration.
public interface ISeedFileSource<TContext> : ISeedFileSource
    where TContext : class;
#pragma warning restore CA1040
