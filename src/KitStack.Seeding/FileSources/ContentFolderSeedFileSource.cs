namespace KitStack.Seeding;

/// <summary>
/// Reads seed files from a folder. A relative folder is resolved against <see cref="AppContext.BaseDirectory"/>, which
/// is the application's output folder (not the current directory, and not this library's own location).
/// </summary>
public sealed class ContentFolderSeedFileSource : ISeedFileSource
{
    /// <summary>Creates a source for <paramref name="path"/>.</summary>
    public ContentFolderSeedFileSource(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Root = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path));
    }

    /// <summary>The absolute folder files are read from.</summary>
    public string Root { get; }

    /// <inheritdoc />
    public string Description => $"folder '{Root}'";

    /// <inheritdoc />
    public Task<Stream?> TryOpenReadAsync(string fileName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(Path.Combine(Root, fileName));
        var root = Path.EndsInDirectorySeparator(Root) ? Root : Root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(root, StringComparison.Ordinal))
            throw new ArgumentException($"Seed file '{fileName}' is outside {Description}.", nameof(fileName));

        if (!File.Exists(fullPath))
            return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        return Task.FromResult<Stream?>(stream);
    }
}
