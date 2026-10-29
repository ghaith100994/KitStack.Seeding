using System.Reflection;

namespace KitStack.Seeding;

/// <summary>Reads seed files embedded as manifest resources, so they ship inside the assembly.</summary>
public sealed class EmbeddedResourceSeedFileSource : ISeedFileSource
{
    private readonly Assembly _assembly;
    private readonly string? _prefix;

    /// <summary>Creates a source over <paramref name="assembly"/>'s resources.</summary>
    /// <param name="assembly">The assembly that embeds the files.</param>
    /// <param name="resourcePrefix">
    /// Optional manifest-name prefix such as <c>"MyApp.Seeding.Files"</c>. Without it, a file matches any resource whose
    /// name ends with <c>.{fileName}</c>; two matches are an error.
    /// </param>
    public EmbeddedResourceSeedFileSource(Assembly assembly, string? resourcePrefix = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        _assembly = assembly;
        _prefix = string.IsNullOrWhiteSpace(resourcePrefix) ? null : resourcePrefix.TrimEnd('.');
    }

    /// <inheritdoc />
    public string Description => _prefix is null
        ? $"embedded resources of '{_assembly.GetName().Name}'"
        : $"embedded resources '{_prefix}.*' of '{_assembly.GetName().Name}'";

    /// <inheritdoc />
    public Task<Stream?> TryOpenReadAsync(string fileName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        cancellationToken.ThrowIfCancellationRequested();

        // Folders in a resource name are separated by dots.
        var normalized = fileName.Replace('\\', '.').Replace('/', '.');
        var names = _assembly.GetManifestResourceNames();
        string[] matches = _prefix is null
            ? [.. names.Where(n => n.EndsWith("." + normalized, StringComparison.OrdinalIgnoreCase) || string.Equals(n, normalized, StringComparison.OrdinalIgnoreCase))]
            : [.. names.Where(n => string.Equals(n, $"{_prefix}.{normalized}", StringComparison.OrdinalIgnoreCase))];

        return matches.Length switch
        {
            0 => Task.FromResult<Stream?>(null),
            1 => Task.FromResult(_assembly.GetManifestResourceStream(matches[0])),
            _ => throw new InvalidOperationException(
                $"Seed file '{fileName}' matches several resources in {Description}: {string.Join(", ", matches)}. Pass a resource prefix."),
        };
    }
}
