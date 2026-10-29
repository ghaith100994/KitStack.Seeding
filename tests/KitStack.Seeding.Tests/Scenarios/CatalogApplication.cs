using KitStack.Seeding.EntityFrameworkCore;
using KitStack.Seeding.Tests.Scenarios.Catalog;
using KitStack.Seeding.Tests.Scenarios.Reference;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KitStack.Seeding.Tests.Scenarios;

/// <summary>
/// The application under test, as a real one would be deployed: SQLite database files on disk, seed files in a
/// folder next to the app (copied there by each "release"), seeders found by assembly scanning, and a startup that
/// migrates and seeds. Each <see cref="StartAsync"/> is a new process start with a new service provider.
/// </summary>
public sealed class CatalogApplication : IAsyncDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kitstack-seeding-" + Guid.NewGuid().ToString("N"));

    public CatalogApplication() => Directory.CreateDirectory(SeedFolder);

    public string SeedFolder => Path.Combine(_root, "Seeding", "Files");

    private string CatalogDb => Path.Combine(_root, "catalog.db");

    private string ReferenceDb => Path.Combine(_root, "reference.db");

    /// <summary>Ships a release: replaces the seed files in the application folder.</summary>
    public void Release(IReadOnlyDictionary<string, string> files)
    {
        foreach (var file in Directory.GetFiles(SeedFolder))
            File.Delete(file);
        foreach (var (name, content) in files)
            File.WriteAllText(Path.Combine(SeedFolder, name), content);
    }

    public ServiceProvider BuildServices(CapturingLoggerProvider logs, Action<SeedingOptions>? configure = null, bool seedFromEfCore = false)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.ClearProviders().AddProvider(logs).SetMinimumLevel(LogLevel.Trace));

        services.AddDbContext<CatalogDbContext>((sp, o) =>
        {
            // Pooling off so the database file can be deleted when the scenario ends.
            o.UseSqlite($"Data Source={CatalogDb};Pooling=False")
             // The hand-written migration has no model snapshot.
             .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
            if (seedFromEfCore)
                o.UseKitStackSeeding<CatalogDbContext>(sp);
        });
        services.AddDbContext<ReferenceDbContext>(o => o.UseSqlite($"Data Source={ReferenceDb};Pooling=False"));

        services.AddSeeding<CatalogDbContext>(o =>
            {
                o.Files.FromContentFolder(SeedFolder);
                configure?.Invoke(o);
            })
            .AddSeedersFromAssemblyContaining<CatalogDbContext>();

        services.AddSeeding<ReferenceDbContext>(o => o.Files.FromEmbeddedResources(typeof(ReferenceDbContext).Assembly, "KitStack.Seeding.Tests.Files.Reference"))
            .AddSeedersFromAssemblyContaining<ReferenceDbContext>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    /// <summary>One application start: migrate and seed the catalog, create and seed the reference data.</summary>
    public async Task<SeedingResult> StartAsync(CapturingLoggerProvider? logs = null, Action<SeedingOptions>? configure = null)
    {
        await using var services = BuildServices(logs ?? new CapturingLoggerProvider(), configure);
        var result = await services.InitializeDatabaseAsync<CatalogDbContext>(DatabaseCreation.Migrate);
        await services.InitializeDatabaseAsync<ReferenceDbContext>(DatabaseCreation.EnsureCreated);
        return result;
    }

    /// <summary>Opens the catalog database outside the application, as an administrator or a user would change it.</summary>
    public async Task<T> CatalogAsync<T>(Func<CatalogDbContext, Task<T>> action)
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlite($"Data Source={CatalogDb};Pooling=False")
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
        await using var db = new CatalogDbContext(options);
        return await action(db);
    }

    public async Task<T> ReferenceAsync<T>(Func<ReferenceDbContext, Task<T>> action)
    {
        await using var db = new ReferenceDbContext(new DbContextOptionsBuilder<ReferenceDbContext>().UseSqlite($"Data Source={ReferenceDb};Pooling=False").Options);
        return await action(db);
    }

    /// <summary>Every table's natural keys, soft-deleted rows included: what a seeding snapshot test compares.</summary>
    public Task<CatalogSnapshot> SnapshotAsync() => CatalogAsync(async db => new CatalogSnapshot(
        await db.Categories.IgnoreQueryFilters().OrderBy(x => x.Code).Select(x => x.Code).ToListAsync(),
        await db.Currencies.OrderBy(x => x.Code).Select(x => x.Code).ToListAsync(),
        await db.Products.IgnoreQueryFilters().OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(),
        await db.Set<ProductTag>().CountAsync()));

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: the temp folder is cleaned by the OS otherwise.
        }

        return ValueTask.CompletedTask;
    }
}

public sealed record CatalogSnapshot(List<string> Categories, List<string> Currencies, List<string> Products, int Tags)
{
    public override string ToString()
        => $"Categories=[{string.Join(",", Categories)}] Currencies=[{string.Join(",", Currencies)}] Products=[{string.Join(",", Products)}] Tags={Tags}";
}
