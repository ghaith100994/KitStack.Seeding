using System.Collections.ObjectModel;
using System.Reflection;

namespace KitStack.Seeding;

/// <summary>The file sources of a context, searched in order: the first source that has a file wins.</summary>
public sealed class SeedFileSourceCollection : Collection<ISeedFileSource>
{
    /// <summary>
    /// Read files from <paramref name="path"/>; a relative path is resolved against <see cref="AppContext.BaseDirectory"/>
    /// (the output folder), not the current directory. Mark the files <c>CopyToOutputDirectory</c>.
    /// </summary>
    public SeedFileSourceCollection FromContentFolder(string path)
    {
        Add(new ContentFolderSeedFileSource(path));
        return this;
    }

    /// <summary>
    /// Read files embedded in <paramref name="assembly"/> (<c>&lt;EmbeddedResource Include="Seeding\Files\*.json" /&gt;</c>).
    /// With <paramref name="resourcePrefix"/> (e.g. <c>"MyApp.Seeding.Files"</c>) a file is looked up by its exact
    /// manifest name; without it, by a resource name ending in <c>.{fileName}</c>.
    /// </summary>
    public SeedFileSourceCollection FromEmbeddedResources(Assembly assembly, string? resourcePrefix = null)
    {
        Add(new EmbeddedResourceSeedFileSource(assembly, resourcePrefix));
        return this;
    }

    /// <summary>Adds any source, e.g. an <see cref="InMemorySeedFileSource"/> in tests.</summary>
    public SeedFileSourceCollection Use(ISeedFileSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Add(source);
        return this;
    }
}
