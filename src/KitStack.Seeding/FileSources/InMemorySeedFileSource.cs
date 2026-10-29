using System.Collections.Concurrent;
using System.Text;

namespace KitStack.Seeding;

/// <summary>Seed files held in memory: for tests, or for data built at startup. File names are case-insensitive.</summary>
public sealed class InMemorySeedFileSource : ISeedFileSource
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public string Description => "in-memory files";

    /// <summary>Adds or replaces <paramref name="fileName"/> with <paramref name="content"/> (UTF-8).</summary>
    public InMemorySeedFileSource Set(string fileName, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);
        _files[fileName] = Encoding.UTF8.GetBytes(content);
        return this;
    }

    /// <summary>Removes <paramref name="fileName"/>.</summary>
    public bool Remove(string fileName) => _files.TryRemove(fileName, out _);

    /// <inheritdoc />
    public Task<Stream?> TryOpenReadAsync(string fileName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<Stream?>(_files.TryGetValue(fileName, out var bytes) ? new MemoryStream(bytes, writable: false) : null);
    }
}
