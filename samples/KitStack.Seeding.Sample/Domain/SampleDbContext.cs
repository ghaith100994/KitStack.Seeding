using System.Text.Json.Serialization;
using KitStack.Seeding;
using Microsoft.EntityFrameworkCore;

namespace KitStack.Seeding.Sample.Domain;

public class Country : ISeedActivatable
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string NameEn { get; set; }
    public string? NameAr { get; set; }

    [JsonIgnore]
    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }
}

public class Currency
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public int DecimalPlaces { get; set; }
}

public class City
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int CountryId { get; set; }
    public Country? Country { get; set; }
    public DateOnly FoundedRecordOn { get; set; }
}

public class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options)
{
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<City> Cities => Set<City>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Country>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Country>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<City>().HasQueryFilter(x => !x.Country!.IsDeleted);
    }
}
