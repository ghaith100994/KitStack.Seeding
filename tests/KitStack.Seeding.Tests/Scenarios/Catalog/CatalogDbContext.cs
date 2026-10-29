using System.Text.Json.Serialization;
using KitStack.Seeding;
using Microsoft.EntityFrameworkCore;

namespace KitStack.Seeding.Tests.Scenarios.Catalog;

public class Category : ISeedActivatable
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";

    [JsonIgnore]
    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }
    public int? ParentId { get; set; }
}

public class Currency
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int DecimalPlaces { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int CategoryId { get; set; }
    public int CurrencyId { get; set; }
    public decimal Price { get; set; }
    public DateOnly ListedOn { get; set; }
    public bool IsDeleted { get; set; }
    public List<ProductTag> Tags { get; set; } = [];
}

public class ProductTag
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Value { get; set; } = "";
}

/// <summary>The application's context in the scenario. Its schema comes from a real migration (<see cref="InitialCreate"/>).</summary>
public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne<Category>().WithMany().HasForeignKey(x => x.ParentId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });
        modelBuilder.Entity<Currency>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Product>(e =>
        {
            e.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId);
            e.HasOne<Currency>().WithMany().HasForeignKey(x => x.CurrencyId);
            e.HasMany(x => x.Tags).WithOne().HasForeignKey(x => x.ProductId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });
    }
}
