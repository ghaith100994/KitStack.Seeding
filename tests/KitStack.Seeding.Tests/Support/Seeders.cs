using System.Text.Json;
using KitStack.Seeding.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KitStack.Seeding.Tests.Support;

public static class TestSeedOrder
{
    public const int Countries = 1;
    public const int Currencies = 2;
    public const int Stages = 3;
    public const int Cities = 100;
}

public class CountriesSeeder(ISeedFileSource<TestDbContext> files, ILogger<CountriesSeeder> logger)
    : LookupSeeder<TestDbContext, Country>(files, logger)
{
    public const string File = "01Countries.json";

    protected override string FileName => File;

    public override int Order => TestSeedOrder.Countries;

    protected override string? KeyOf(Country entity) => entity.Code;

    // Names are unique too: a new code with a taken name is skipped.
    protected override bool ConflictsWith(Country entity, IReadOnlyList<Country> existing)
        => existing.Any(x => string.Equals(x.Name, entity.Name, StringComparison.OrdinalIgnoreCase));
}

public class CurrenciesSeeder(ISeedFileSource<TestDbContext> files, ILogger<CurrenciesSeeder> logger)
    : JsonSeeder<TestDbContext, Currency>(files, logger)
{
    public const string File = "02Currencies.json";

    protected override string FileName => File;

    public override int Order => TestSeedOrder.Currencies;

    protected override Task OnBeforeSaveAsync(TestDbContext context, List<Currency> entities, CancellationToken cancellationToken)
    {
        foreach (var currency in entities)
            currency.Code = currency.Code?.ToUpperInvariant();
        return Task.CompletedTask;
    }
}

/// <summary>Links each new stage to its next stage by code after the insert.</summary>
public class StagesSeeder(ISeedFileSource<TestDbContext> files, ILogger<StagesSeeder> logger)
    : LookupSeeder<TestDbContext, Stage>(files, logger)
{
    public const string File = "03Stages.json";

    protected override string FileName => File;

    public override int Order => TestSeedOrder.Stages;

    protected override string? KeyOf(Stage entity) => entity.Code;

    protected override async Task OnInsertedAsync(TestDbContext context, IReadOnlyList<SeedRow<Stage>> inserted, CancellationToken cancellationToken)
    {
        var byCode = await context.Stages.ToDictionaryAsync(x => x.Code!, StringComparer.OrdinalIgnoreCase, cancellationToken);
        foreach (var row in inserted)
        {
            if (row.GetString("NextStageCode") is { } next && byCode.TryGetValue(next, out var stage))
                row.Entity.NextStageId = stage.Id;
        }
    }
}

public class CitiesSeeder(ISeedFileSource<TestDbContext> files, ILogger<CitiesSeeder> logger)
    : ResolvingSeeder<TestDbContext, City, CitiesSeeder.Row>(files, logger)
{
    public const string File = "100Cities.json";

    protected override string FileName => File;

    public override int Order => TestSeedOrder.Cities;

    // Cities need countries: until the countries exist the seeder waits for a later run.
    protected override Task<bool> IsReadyAsync(TestDbContext context, CancellationToken cancellationToken)
        => context.Countries.AnyAsync(cancellationToken);

    protected override async Task<City?> ResolveAsync(Row row, TestDbContext context, CancellationToken cancellationToken)
    {
        var country = await FindAsync<Country>(context, x => x.Code, row.CountryCode, cancellationToken);
        if (country is null)
        {
            Skip(row.Name, $"country {row.CountryCode} not found");
            return null;
        }

        var city = new City { Name = row.Name, CountryId = country.Id };
        city.Districts.AddRange(row.Districts.Select(d => new District { Name = d }));
        return city;
    }

    public sealed class Row
    {
        public string? Name { get; set; }
        public string? CountryCode { get; set; }
        public List<string> Districts { get; set; } = [];
    }
}

/// <summary>Adds a row and then throws, to check that the half-added row is not saved by the next seeder.</summary>
public class BrokenSeeder(ISeedFileSource<TestDbContext> files, ILogger<BrokenSeeder> logger) : SeederBase<TestDbContext>(files, logger)
{
    public override int Order => 0;

    protected override Task SeedCoreAsync(TestDbContext context, CancellationToken cancellationToken)
    {
        context.Currencies.Add(new Currency { Code = "BROKEN", Name = "Half added" });
        throw new InvalidOperationException("boom");
    }
}

public static class TestJson
{
    public static string Of(object value) => JsonSerializer.Serialize(value);
}
