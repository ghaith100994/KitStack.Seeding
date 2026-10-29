using KitStack.Seeding.Tests.Scenarios.Catalog;

namespace KitStack.Seeding.Tests.Scenarios;

/// <summary>The seed files the application ships in each release.</summary>
public static class CatalogReleases
{
    public static Dictionary<string, string> Release1 { get; } = new()
    {
        [CategoriesSeeder.File] = """
            // Release 1 categories. Codes are the natural key: never rename them.
            [
              { "Code": "ELEC", "Name": "Electronics" },
              { "Code": "PHONE", "Name": "Phones", "ParentCode": "ELEC" },
              { "Code": "BOOK", "Name": "Books" },
              { "Code": "TOY", "Name": "Toys", "IsActive": false },
            ]
            """,
        [CurrenciesSeeder.File] = """
            [
              { "Code": "USD", "Name": "US dollar", "DecimalPlaces": 2 },
              { "Code": "EUR", "Name": "Euro", "DecimalPlaces": 2 }
            ]
            """,
        [DemoProductsSeeder.File] = """
            [
              { "Name": "Phone X", "CategoryCode": "phone", "CurrencyCode": "USD", "Price": 499.99, "ListedInDays": -10, "Tags": ["new", "5g"] },
              { "Name": "Novel", "CategoryCode": "BOOK", "CurrencyCode": "EUR", "Price": 12.5, "ListedInDays": -400 },
              { "Name": "Mystery item", "CategoryCode": "NOPE", "CurrencyCode": "USD", "Price": 1, "ListedInDays": 0 }
            ]
            """,
    };

    /// <summary>
    /// Release 2 edits every file: new categories (one renamed by mistake, one clashing on name, one repeated), a
    /// changed name for an existing code, a new currency and a new demo product.
    /// </summary>
    public static Dictionary<string, string> Release2 { get; } = new()
    {
        [CategoriesSeeder.File] = """
            [
              { "Code": "ELEC", "Name": "Electronics & Gadgets" },
              { "Code": "PHONE", "Name": "Phones", "ParentCode": "ELEC" },
              { "Code": "BOOK", "Name": "Books" },
              { "Code": "TOY", "Name": "Toys", "IsActive": false },
              { "Code": "LAPTOP", "Name": "Laptops", "ParentCode": "ELEC" },
              { "Code": "EBOOK", "Name": "E-books", "ParentCode": "BOOK" },
              { "Code": "BOOKS", "Name": "books" },
              { "Code": "laptop", "Name": "Laptops again" },
              { "Code": "TABLET", "Name": "Tablets", "ParentCode": "MISSING" },
            ]
            """,
        [CurrenciesSeeder.File] = """
            [
              { "Code": "USD", "Name": "US dollar", "DecimalPlaces": 2 },
              { "Code": "EUR", "Name": "Euro", "DecimalPlaces": 2 },
              { "Code": "GBP", "Name": "Pound sterling", "DecimalPlaces": 2 }
            ]
            """,
        [DemoProductsSeeder.File] = """
            [
              { "Name": "Phone X", "CategoryCode": "PHONE", "CurrencyCode": "USD", "Price": 499.99, "ListedInDays": -10 },
              { "Name": "Laptop Pro", "CategoryCode": "LAPTOP", "CurrencyCode": "USD", "Price": 1999, "ListedInDays": -1 }
            ]
            """,
    };

    /// <summary>A release whose category file is broken.</summary>
    public static Dictionary<string, string> BrokenCategories { get; } = new(Release2)
    {
        [CategoriesSeeder.File] = """[ { "Code": "BROKEN", "Name": ]""",
    };
}
