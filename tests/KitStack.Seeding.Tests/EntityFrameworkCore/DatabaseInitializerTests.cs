using KitStack.Seeding.EntityFrameworkCore;
using KitStack.Seeding.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KitStack.Seeding.Tests.EntityFrameworkCore;

public class DatabaseInitializerTests
{
    private const string Countries = """[{ "Code": "SY", "Name": "Syria" }]""";

    [Theory]
    [InlineData(DatabaseCreation.EnsureCreated)]
    [InlineData(DatabaseCreation.Migrate)]
    public async Task Creates_the_database_and_seeds(DatabaseCreation creation)
    {
        await using var fixture = await SeedingFixture.CreateAsync(b => b.AddSeeder<CountriesSeeder>(), createDatabase: false);
        fixture.Files.Set(CountriesSeeder.File, Countries);

        if (creation == DatabaseCreation.Migrate)
        {
            // The test model has no migrations: create the schema first, then Migrate finds nothing pending.
            await fixture.ExecuteAsync(db => db.Database.EnsureCreatedAsync());
        }

        var result = await fixture.Services.InitializeDatabaseAsync<TestDbContext>(creation);

        Assert.True(result.Succeeded);
        Assert.Equal(1, await fixture.QueryAsync(db => db.Countries.CountAsync()));
    }

    [Fact]
    public async Task None_only_seeds()
    {
        await using var fixture = await SeedingFixture.CreateAsync(b => b.AddSeeder<CountriesSeeder>());
        fixture.Files.Set(CountriesSeeder.File, Countries);

        await fixture.Services.InitializeDatabaseAsync<TestDbContext>(DatabaseCreation.None);

        Assert.Equal(1, await fixture.QueryAsync(db => db.Countries.CountAsync()));
    }

    [Fact]
    public async Task A_registered_initializer_is_used()
    {
        await using var fixture = await SeedingFixture.CreateAsync(b =>
        {
            b.AddSeeder<CountriesSeeder>();
            b.Services.AddScoped<DatabaseInitializer<TestDbContext>, CountingInitializer>();
        });
        fixture.Files.Set(CountriesSeeder.File, Countries);

        await fixture.Services.InitializeDatabaseAsync<TestDbContext>(DatabaseCreation.None);

        Assert.Equal(1, CountingInitializer.Calls);
    }

    [Fact]
    public async Task UseKitStackSeeding_seeds_from_EnsureCreated()
    {
        await using var fixture = await SeedingFixture.CreateAsync(
            b => b.AddSeeder<CountriesSeeder>(),
            dbOptions: (sp, o) => o.UseKitStackSeeding<TestDbContext>(sp),
            createDatabase: false);
        fixture.Files.Set(CountriesSeeder.File, Countries);

        await fixture.ExecuteAsync(db => db.Database.EnsureCreatedAsync());

        Assert.Equal(1, await fixture.QueryAsync(db => db.Countries.CountAsync()));
    }

    [Fact]
    public async Task UseKitStackSeeding_seeds_from_the_synchronous_EnsureCreated()
    {
        await using var fixture = await SeedingFixture.CreateAsync(
            b => b.AddSeeder<CountriesSeeder>(),
            dbOptions: (sp, o) => o.UseKitStackSeeding<TestDbContext>(sp),
            createDatabase: false);
        fixture.Files.Set(CountriesSeeder.File, Countries);

        await fixture.ExecuteAsync(db =>
        {
            db.Database.EnsureCreated();
            return Task.CompletedTask;
        });

        Assert.Equal(1, await fixture.QueryAsync(db => db.Countries.CountAsync()));
    }

    private sealed class CountingInitializer(TestDbContext context, ISeederRunner<TestDbContext> runner, Microsoft.Extensions.Logging.ILogger<DatabaseInitializer<TestDbContext>> logger)
        : DatabaseInitializer<TestDbContext>(context, runner, logger)
    {
        public static int Calls { get; private set; }

        public override Task<SeedingResult> InitializeAsync(DatabaseCreation creation = DatabaseCreation.Migrate, CancellationToken cancellationToken = default)
        {
            Calls++;
            return base.InitializeAsync(creation, cancellationToken);
        }
    }
}
