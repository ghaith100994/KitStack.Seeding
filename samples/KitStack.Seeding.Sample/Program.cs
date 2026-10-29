using KitStack.Seeding;
using KitStack.Seeding.EntityFrameworkCore;
using KitStack.Seeding.Sample.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Run it twice: the second run adds nothing. Add a country to Seeding/Files/01Countries.json and run again:
// only the new country is inserted.
var services = new ServiceCollection();
services.AddLogging(b => b.AddSimpleConsole(o => o.SingleLine = true)
    .SetMinimumLevel(LogLevel.Information)
    .AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning));
services.AddDbContext<SampleDbContext>(o => o.UseSqlite($"Data Source={Path.Combine(AppContext.BaseDirectory, "sample.db")}"));

services.AddSeeding<SampleDbContext>(o =>
    {
        o.FailurePolicy = SeedFailurePolicy.Throw;
        o.Files.FromContentFolder("Seeding/Files");
    })
    .AddSeedersFromAssemblyContaining<Program>();

await using var provider = services.BuildServiceProvider();

// The sample has no migrations, so it creates the schema from the model.
var result = await provider.InitializeDatabaseAsync<SampleDbContext>(DatabaseCreation.EnsureCreated);
foreach (var seeder in result.Seeders)
    Console.WriteLine($"{seeder.Order,4}  {seeder.Name,-18} {seeder.Status,-10} {seeder.Duration.TotalMilliseconds,6:0} ms");

await using var scope = provider.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
Console.WriteLine();
Console.WriteLine($"Countries: {string.Join(", ", await db.Countries.OrderBy(x => x.Code).Select(x => x.IsActive ? x.Code : x.Code + " (inactive)").ToListAsync())}");
Console.WriteLine($"Currencies: {string.Join(", ", await db.Currencies.OrderBy(x => x.Code).Select(x => x.Code).ToListAsync())}");
Console.WriteLine($"Cities: {string.Join(", ", await db.Cities.OrderBy(x => x.Name).Select(x => x.Name).ToListAsync())}");
