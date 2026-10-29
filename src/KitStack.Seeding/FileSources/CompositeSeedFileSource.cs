namespace KitStack.Seeding;

/// <summary>Searches several sources in order; the first source that has the file wins.</summary>
public sealed class CompositeSeedFileSource : ISeedFileSource
{
    private readonly ISeedFileSource[] _sources;

    /// <summary>Creates a source over <paramref name="sources"/>.</summary>
    public CompositeSeedFileSource(IEnumerable<ISeedFileSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        _sources = [.. sources];
    }

    /// <summary>The sources, in lookup order.</summary>
    public IReadOnlyList<ISeedFileSource> Sources => _sources;

    /// <inheritdoc />
    public string Description => _sources.Length == 0 ? "no sources" : string.Join(", ", _sources.Select(x => x.Description));

    /// <inheritdoc />
    public async Task<Stream?> TryOpenReadAsync(string fileName, CancellationToken cancellationToken)
    {
        foreach (var source in _sources)
        {
            var stream = await source.TryOpenReadAsync(fileName, cancellationToken).ConfigureAwait(false);
            if (stream is not null)
                return stream;
        }

        return null;
    }
}
