using KitStack.Seeding.EntityFrameworkCore;
using KitStack.Seeding.Sample.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KitStack.Seeding.Sample.Seeding;

/// <summary>One place for the run order of the context's seeders. Lookups first, data that references them from 100.</summary>
public static class SampleSeedOrder
{
    public const int Countries = 1;
    public const int Currencies = 2;

    public const int SecondPhaseStart = 100;
    public const int Cities = SecondPhaseStart;
}

/// <summary>Additive by code: countries added to the file in a later release reach existing databases.</summary>
public class CountriesSeeder(ISeedFileSource<SampleDbContext> files, ILogger<CountriesSeeder> logger)
    : LookupSeeder<SampleDbContext, Country>(files, logger)
{
    protected override string FileName => "01Countries.json";

    public override int Order => SampleSeedOrder.Countries;

    protected override string? KeyOf(Country entity) => entity.Code;
}

/// <summary>Inserted once, while the table is empty.</summary>
public class CurrenciesSeeder(ISeedFileSource<SampleDbContext> files, ILogger<CurrenciesSeeder> logger)
    : JsonSeeder<SampleDbContext, Currency>(files, logger)
{
    protected override string FileName => "02Currencies.json";

    public override int Order => SampleSeedOrder.Currencies;
}

/// <summary>Each row names its country by code; dates are day offsets from today.</summary>
public class CitiesSeeder(ISeedFileSource<SampleDbContext> files, ILogger<CitiesSeeder> logger)
    : ResolvingSeeder<SampleDbContext, City, CitiesSeeder.Row>(files, logger)
{
    protected override string FileName => "100Cities.json";

    public override int Order => SampleSeedOrder.Cities;

    protected override Task<bool> IsReadyAsync(SampleDbContext context, CancellationToken cancellationToken)
        => context.Countries.AnyAsync(cancellationToken);

    protected override async Task<City?> ResolveAsync(Row row, SampleDbContext context, CancellationToken cancellationToken)
    {
        var country = await FindAsync<Country>(context, x => x.Code, row.CountryCode, cancellationToken);
        if (country is null || row.Name is null)
        {
            Skip(row.Name, $"country {row.CountryCode} not found");
            return null;
        }

        return new City { Name = row.Name, CountryId = country.Id, FoundedRecordOn = SeedDates.Day(row.RecordedInDays) };
    }

    public sealed class Row
    {
        public string? Name { get; set; }
        public string? CountryCode { get; set; }
        public int RecordedInDays { get; set; }
    }
}
