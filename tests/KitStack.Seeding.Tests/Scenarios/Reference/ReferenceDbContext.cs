using KitStack.Seeding.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KitStack.Seeding.Tests.Scenarios.Reference;

public class Country
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

/// <summary>A second context in the same application. Its seed files are embedded resources, not a folder.</summary>
public class ReferenceDbContext(DbContextOptions<ReferenceDbContext> options) : DbContext(options)
{
    public DbSet<Country> Countries => Set<Country>();
}

public class CountriesSeeder(ISeedFileSource<ReferenceDbContext> files, ILogger<CountriesSeeder> logger)
    : LookupSeeder<ReferenceDbContext, Country>(files, logger)
{
    protected override string FileName => "01Countries.json";

    public override int Order => 1;

    protected override string? KeyOf(Country entity) => entity.Code;
}
