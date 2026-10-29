using KitStack.Seeding.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace KitStack.Seeding.Tests.EntityFrameworkCore;

public class ResolvingSeederTests
{
    private const string Countries = """[{ "Code": "SY", "Name": "Syria" }, { "Code": "JO", "Name": "Jordan" }]""";

    private const string Cities = """
        [
          { "Name": "Damascus", "CountryCode": "sy", "Districts": ["Old City", "Mazzeh"] },
          { "Name": "Amman", "CountryCode": "JO" },
          { "Name": "Nowhere", "CountryCode": "XX" }
        ]
        """;

    private static async Task<SeedingFixture> CreateAsync(bool withCountries = true)
    {
        var fixture = await SeedingFixture.CreateAsync(b => b.AddSeeder<CountriesSeeder>().AddSeeder<CitiesSeeder>());
        fixture.Files.Set(CountriesSeeder.File, withCountries ? Countries : "[]");
        fixture.Files.Set(CitiesSeeder.File, Cities);
        return fixture;
    }

    [Fact]
    public async Task Resolves_references_by_key_and_skips_unresolved_rows()
    {
        await using var fixture = await CreateAsync();

        await fixture.RunAsync();

        var cities = await fixture.QueryAsync(db => db.Cities.Include(x => x.Country).Include(x => x.Districts).OrderBy(x => x.Name).ToListAsync());
        Assert.Equal(["Amman", "Damascus"], cities.Select(x => x.Name));
        Assert.Equal("SY", cities[1].Country!.Code);
        Assert.Equal(["Mazzeh", "Old City"], cities[1].Districts.Select(x => x.Name).Order());
    }

    [Fact]
    public async Task Waits_while_its_prerequisites_are_missing()
    {
        await using var fixture = await CreateAsync(withCountries: false);

        var result = await fixture.RunAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(0, await fixture.QueryAsync(db => db.Cities.CountAsync()));

        fixture.Files.Set(CountriesSeeder.File, Countries);
        await fixture.RunAsync();

        Assert.Equal(2, await fixture.QueryAsync(db => db.Cities.CountAsync()));
    }

    [Fact]
    public async Task Runs_only_while_the_table_is_empty()
    {
        await using var fixture = await CreateAsync();
        await fixture.RunAsync();

        fixture.Files.Set(CitiesSeeder.File, """[{ "Name": "Aleppo", "CountryCode": "SY" }]""");
        await fixture.RunAsync();

        Assert.Equal(2, await fixture.QueryAsync(db => db.Cities.CountAsync()));
    }

    [Fact]
    public async Task Soft_deleted_references_are_not_resolved()
    {
        await using var fixture = await SeedingFixture.CreateAsync(b => b.AddSeeder<CitiesSeeder>());
        fixture.Files.Set(CitiesSeeder.File, Cities);
        await fixture.ExecuteAsync(db =>
        {
            db.Countries.AddRange(new Country { Code = "SY", Name = "Syria", IsDeleted = true }, new Country { Code = "JO", Name = "Jordan" });
            return db.SaveChangesAsync();
        });

        await fixture.RunAsync();

        Assert.Equal(["Amman"], await fixture.QueryAsync(db => db.Cities.IgnoreQueryFilters().Select(x => x.Name!).ToListAsync()));
    }
}
