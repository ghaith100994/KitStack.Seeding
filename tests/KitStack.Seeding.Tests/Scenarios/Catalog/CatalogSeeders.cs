using KitStack.Seeding.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KitStack.Seeding.Tests.Scenarios.Catalog;

public static class CatalogSeedOrder
{
    public const int Categories = 1;
    public const int Currencies = 2;

    public const int SecondPhaseStart = 100;
    public const int DemoProducts = SecondPhaseStart;
}

/// <summary>Additive by code; names are unique too; a category names its parent by code.</summary>
public class CategoriesSeeder(ISeedFileSource<CatalogDbContext> files, ILogger<CategoriesSeeder> logger)
    : LookupSeeder<CatalogDbContext, Category>(files, logger)
{
    public const string File = "01Categories.json";

    protected override string FileName => File;

    public override int Order => CatalogSeedOrder.Categories;

    protected override string? KeyOf(Category entity) => entity.Code;

    protected override bool ConflictsWith(Category entity, IReadOnlyList<Category> existing)
        => existing.Any(x => string.Equals(x.Name, entity.Name, StringComparison.OrdinalIgnoreCase));

    protected override async Task OnInsertedAsync(CatalogDbContext context, IReadOnlyList<SeedRow<Category>> inserted, CancellationToken cancellationToken)
    {
        var byCode = await context.Categories.ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        foreach (var row in inserted)
        {
            if (row.GetString("ParentCode") is not { } parent)
                continue;
            if (byCode.TryGetValue(parent, out var category))
                row.Entity.ParentId = category.Id;
            else
                Skip(row.Entity.Code, $"parent {parent} not found");
        }
    }
}

/// <summary>Seeded once; afterwards currencies belong to the users.</summary>
public class CurrenciesSeeder(ISeedFileSource<CatalogDbContext> files, ILogger<CurrenciesSeeder> logger)
    : JsonSeeder<CatalogDbContext, Currency>(files, logger)
{
    public const string File = "02Currencies.json";

    protected override string FileName => File;

    public override int Order => CatalogSeedOrder.Currencies;
}

/// <summary>Demo data: products reference categories and currencies by code, dates are offsets from today.</summary>
public class DemoProductsSeeder(ISeedFileSource<CatalogDbContext> files, ILogger<DemoProductsSeeder> logger)
    : ResolvingSeeder<CatalogDbContext, Product, DemoProductsSeeder.Row>(files, logger)
{
    public const string File = "100DemoProducts.json";

    protected override string FileName => File;

    public override int Order => CatalogSeedOrder.DemoProducts;

    protected override async Task<bool> IsReadyAsync(CatalogDbContext context, CancellationToken cancellationToken)
        => await context.Categories.AnyAsync(cancellationToken) && await context.Currencies.AnyAsync(cancellationToken);

    protected override async Task<Product?> ResolveAsync(Row row, CatalogDbContext context, CancellationToken cancellationToken)
    {
        var category = await FindAsync<Category>(context, x => x.Code, row.CategoryCode, cancellationToken);
        var currency = await FindAsync<Currency>(context, x => x.Code, row.CurrencyCode, cancellationToken);
        if (category is null || currency is null)
        {
            Skip(row.Name, $"category {row.CategoryCode} or currency {row.CurrencyCode} not found");
            return null;
        }

        return new Product
        {
            Name = row.Name,
            CategoryId = category.Id,
            CurrencyId = currency.Id,
            Price = row.Price,
            ListedOn = SeedDates.Day(row.ListedInDays),
            Tags = [.. row.Tags.Select(t => new ProductTag { Value = t })],
        };
    }

    public sealed class Row
    {
        public string Name { get; set; } = "";
        public string? CategoryCode { get; set; }
        public string? CurrencyCode { get; set; }
        public decimal Price { get; set; }
        public int ListedInDays { get; set; }
        public List<string> Tags { get; set; } = [];
    }
}
