using KitStack.Seeding;

namespace KitStack.Seeding.Tests.Core;

public class FileSourceTests
{
    private sealed record Color(string Code, string Name);

    [Fact]
    public async Task ContentFolder_resolves_relative_paths_against_the_output_folder()
    {
        var source = new ContentFolderSeedFileSource("Files");

        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "Files"), source.Root);
        var colors = await source.ReadListAsync<Color>("01Colors.json");
        Assert.Equal(["RED", "BLUE"], colors.Select(x => x.Code));
    }

    [Fact]
    public async Task ContentFolder_returns_null_for_a_missing_file()
    {
        var source = new ContentFolderSeedFileSource("Files");

        Assert.Null(await source.TryOpenReadAsync("missing.json", CancellationToken.None));
    }

    [Theory]
    [InlineData("../01Colors.json")]
    [InlineData("../../secrets.json")]
    public async Task ContentFolder_rejects_paths_outside_the_folder(string fileName)
    {
        var source = new ContentFolderSeedFileSource("Files");

        await Assert.ThrowsAsync<ArgumentException>(() => source.TryOpenReadAsync(fileName, CancellationToken.None));
    }

    [Fact]
    public async Task EmbeddedResource_finds_a_file_by_its_name()
    {
        var source = new EmbeddedResourceSeedFileSource(typeof(FileSourceTests).Assembly);

        var colors = await source.ReadListAsync<Color>("01Colors.json");
        Assert.Equal(2, colors.Count);
    }

    [Fact]
    public async Task EmbeddedResource_with_prefix_requires_the_exact_name()
    {
        var withPrefix = new EmbeddedResourceSeedFileSource(typeof(FileSourceTests).Assembly, "KitStack.Seeding.Tests.Files");
        var wrongPrefix = new EmbeddedResourceSeedFileSource(typeof(FileSourceTests).Assembly, "Other.Files");

        Assert.Equal(2, (await withPrefix.ReadListAsync<Color>("01Colors.json")).Count);
        Assert.Null(await wrongPrefix.TryOpenReadAsync("01Colors.json", CancellationToken.None));
    }

    [Fact]
    public async Task InMemory_names_are_case_insensitive()
    {
        var source = new InMemorySeedFileSource().Set("a.json", "[1,2]");

        Assert.Equal([1, 2], await source.ReadListAsync<int>("A.JSON"));
        Assert.True(source.Remove("a.json"));
        Assert.Null(await source.TryOpenReadAsync("a.json", CancellationToken.None));
    }

    [Fact]
    public async Task Composite_returns_the_first_source_that_has_the_file()
    {
        var first = new InMemorySeedFileSource().Set("a.json", "[1]");
        var second = new InMemorySeedFileSource().Set("a.json", "[2]").Set("b.json", "[3]");
        var source = new CompositeSeedFileSource([first, second]);

        Assert.Equal([1], await source.ReadListAsync<int>("a.json"));
        Assert.Equal([3], await source.ReadListAsync<int>("b.json"));
    }

    [Fact]
    public async Task Missing_file_names_every_place_it_was_looked_for()
    {
        var source = new CompositeSeedFileSource([new InMemorySeedFileSource(), new ContentFolderSeedFileSource("Files")]);

        var ex = await Assert.ThrowsAsync<SeedFileNotFoundException>(() => source.ReadListAsync<int>("missing.json"));
        Assert.Equal("missing.json", ex.FileName);
        Assert.Contains("in-memory files", ex.Message, StringComparison.Ordinal);
        Assert.Contains("folder '", ex.Message, StringComparison.Ordinal);
    }
}
