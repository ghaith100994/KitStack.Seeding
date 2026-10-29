using KitStack.Seeding;
using KitStack.Seeding.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KitStack.Seeding.Tests.Support;

/// <summary>A service provider over a private SQLite in-memory database, with in-memory seed files.</summary>
public sealed class SeedingFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private SeedingFixture(SqliteConnection connection, ServiceProvider services, InMemorySeedFileSource files)
    {
        _connection = connection;
        Services = services;
        Files = files;
    }

    public ServiceProvider Services { get; }

    public InMemorySeedFileSource Files { get; }

    public static async Task<SeedingFixture> CreateAsync(
        Action<SeedingBuilder<TestDbContext>>? seeders = null,
        Action<SeedingOptions>? configure = null,
        Action<IServiceProvider, DbContextOptionsBuilder>? dbOptions = null,
        bool createDatabase = true)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var files = new InMemorySeedFileSource();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TestDbContext>((sp, o) =>
        {
            o.UseSqlite(connection);
            dbOptions?.Invoke(sp, o);
        });
        var builder = services.AddSeeding<TestDbContext>(o =>
        {
            o.Files.Use(files);
            configure?.Invoke(o);
        });
        seeders?.Invoke(builder);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        if (createDatabase)
        {
            await using var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<TestDbContext>().Database.EnsureCreatedAsync();
        }

        return new SeedingFixture(connection, provider, files);
    }

    public Task<SeedingResult> RunAsync() => Services.RunSeedersAsync<TestDbContext>();

    /// <summary>Runs <paramref name="action"/> against a fresh context.</summary>
    public async Task<T> QueryAsync<T>(Func<TestDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<TestDbContext>());
    }

    public async Task ExecuteAsync(Func<TestDbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<TestDbContext>());
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
