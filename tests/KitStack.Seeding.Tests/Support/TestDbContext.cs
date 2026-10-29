using System.Text.Json.Serialization;
using KitStack.Seeding;
using Microsoft.EntityFrameworkCore;

namespace KitStack.Seeding.Tests.Support;

public enum CountryKind
{
    Republic,
    Kingdom,
}

public class Country : ISeedActivatable
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public CountryKind Kind { get; set; }

    // Not bound from JSON: the lookup seeder reads it from each row.
    [JsonIgnore]
    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }
}

public class Currency
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public bool IsDeleted { get; set; }
}

public class Stage
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public int? NextStageId { get; set; }
    public Stage? NextStage { get; set; }
}

public class City
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int CountryId { get; set; }
    public Country? Country { get; set; }
    public List<District> Districts { get; set; } = [];
}

public class District
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int CityId { get; set; }
}

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<Stage> Stages => Set<Stage>();
    public DbSet<City> Cities => Set<City>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Country>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<Country>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Currency>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<Currency>().Property(x => x.Code).IsRequired();
        modelBuilder.Entity<Stage>().HasOne(x => x.NextStage).WithMany().HasForeignKey(x => x.NextStageId);
        modelBuilder.Entity<City>().HasQueryFilter(x => !x.Country!.IsDeleted);
    }
}
