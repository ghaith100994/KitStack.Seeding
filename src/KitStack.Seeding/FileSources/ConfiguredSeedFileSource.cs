using Microsoft.Extensions.Options;

namespace KitStack.Seeding;

/// <summary>The <see cref="ISeedFileSource{TContext}"/> built from <see cref="SeedingOptions.Files"/> of the context.</summary>
internal sealed class ConfiguredSeedFileSource<TContext>(IOptionsMonitor<SeedingOptions> options) : ISeedFileSource<TContext>
    where TContext : class
{
    private readonly Lazy<CompositeSeedFileSource> _source = new(() =>
    {
        var files = options.Get(SeedingOptions.NameFor<TContext>()).Files;
        return new CompositeSeedFileSource(files.Count > 0 ? files : [new ContentFolderSeedFileSource(SeedingOptions.DefaultContentFolder)]);
    });

    public string Description => _source.Value.Description;

    public Task<Stream?> TryOpenReadAsync(string fileName, CancellationToken cancellationToken)
        => _source.Value.TryOpenReadAsync(fileName, cancellationToken);
}
