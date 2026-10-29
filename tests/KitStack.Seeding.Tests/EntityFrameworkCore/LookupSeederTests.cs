using KitStack.Seeding.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace KitStack.Seeding.Tests.EntityFrameworkCore;

public class LookupSeederTests
{
    private static async Task<SeedingFixture> CreateAsync()
    {
        var fixture = await SeedingFixture.CreateAsync(b => b.AddSeeder<CountriesSeeder>().AddSeeder<StagesSeeder>());
        fixture.Files.Set(CountriesSeeder.File, "[]").Set(StagesSeeder.File, "[]");
        return fixture;
    }

    private static Task<List<string>> CodesAsync(SeedingFixture fixture)
        => fixture.QueryAsync(db => db.Countries.IgnoreQueryFilters().OrderBy(x => x.Code).Select(x => x.Code!).ToListAsync());

    [Fact]
    public async Task Rows_added_to_the_file_later_are_inserted()
    {
        await using var fixture = await CreateAsync();
        fixture.Files.Set(CountriesSeeder.File, """[{ "Code": "SY", "Name": "Syria" }]""");
        await fixture.RunAsync();

        fixture.Files.Set(CountriesSeeder.File, """[{ "Code": "SY", "Name": "Syria" }, { "Code": "JO", "Name": "Jordan" }]""");
        await fixture.RunAsync();

        Assert.Equal(["JO", "SY"], await CodesAsync(fixture));
    }

    [Fact]
    public async Task Running_twice_adds_nothing()
    {
        await using var fixture = await CreateAsync();
        fixture.Files.Set(CountriesSeeder.File, """[{ "Code": "SY", "Name": "Syria" }, { "Code": "JO", "Name": "Jordan" }]""");

        await fixture.RunAsync();
        await fixture.RunAsync();

        Assert.Equal(2, await fixture.QueryAsync(db => db.Countries.CountAsync()));
    }

    [Fact]
    public async Task Existing_rows_are_not_overwritten()
    {
        await using var fixture = await CreateAsync();
        fixture.Files.Set(CountriesSeeder.File, """[{ "Code": "SY", "Name": "Syria" }]""");
        await fixture.RunAsync();
        await fixture.ExecuteAsync(db => db.Countries.ExecuteUpdateAsync(s => s.SetProperty(x => x.Name, "Edited by an admin")));

        fixture.Files.Set(CountriesSeeder.File, """[{ "Code": "SY", "Name": "Syrian Arab Republic" }]""");
        await fixture.RunAsync();

        Assert.Equal("Edited by an admin", await fixture.QueryAsync(db => db.Countries.Select(x => x.Name).SingleAsync()));
    }

    [Fact]
    public async Task Soft_deleted_rows_are_not_brought_back()
    {
        await using var fixture = await CreateAsync();
        fixture.Files.Set(CountriesSeeder.File, """[{ "Code": "SY", "Name": "Syria" }, { "Code": "JO", "Name": "Jordan" }]""");
        await fixture.RunAsync();
        await fixture.ExecuteAsync(db => db.Countries.Where(x => x.Code == "JO").ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDeleted, true)));

        await fixture.RunAsync();

        Assert.Equal(["JO", "SY"], await CodesAsync(fixture));
        Assert.Equal(1, await fixture.QueryAsync(db => db.Countries.CountAsync()));
    }

    [Fact]
    public async Task Keys_are_matched_trimmed_and_case_insensitively()
    {
        await using var fixture = await CreateAsync();
        fixture.Files.Set(CountriesSeeder.File, """[{ "Code": "SY", "Name": "Syria" }]""");
        await fixture.RunAsync();

        fixture.Files.Set(CountriesSeeder.File, """[{ "Code": " sy ", "Name": "Syria again" }]""");
        await fixture.RunAsync();

        Assert.Equal(["SY"], await CodesAsync(fixture));
    }

    [Fact]
    public async Task IsActive_is_read_from_each_row_even_when_JsonIgnored()
    {
        await using var fixture = await CreateAsync();
        fixture.Files.Set(CountriesSeeder.File, """
            [
              { "Code": "SY", "Name": "Syria" },
              { "Code": "JO", "Name": "Jordan", "IsActive": true },
              { "Code": "LB", "Name": "Lebanon", "isActive": false }
            ]
            """);

        await fixture.RunAsync();

        var active = await fixture.QueryAsync(db => db.Countries.OrderBy(x => x.Code).Select(x => new { x.Code, x.IsActive }).ToListAsync());
        Assert.Equal([("JO", true), ("LB", false), ("SY", true)], active.Select(x => (x.Code!, x.IsActive)));
    }

    [Fact]
    public async Task Rows_without_a_key_and_repeated_keys_are_skipped()
    {
        await using var fixture = await CreateAsync();
        fixture.Files.Set(CountriesSeeder.File, """
            [
              { "Name": "No code" },
              { "Code": "  ", "Name": "Blank code" },
              { "Code": "SY", "Name": "Syria" },
              { "Code": "sy", "Name": "Syria twice" }
            ]
            """);

        await fixture.RunAsync();

        Assert.Equal("Syria", await fixture.QueryAsync(db => db.Countries.Select(x => x.Name).SingleAsync()));
    }

    [Fact]
    public async Task ConflictsWith_skips_rows_that_clash_with_existing_or_new_rows()
    {
        await using var fixture = await CreateAsync();
        fixture.Files.Set(CountriesSeeder.File, """[{ "Code": "SY", "Name": "Syria" }]""");
        await fixture.RunAsync();

        fixture.Files.Set(CountriesSeeder.File, """
            [
              { "Code": "SYR", "Name": "SYRIA" },
              { "Code": "JO", "Name": "Jordan" },
              { "Code": "JOR", "Name": "Jordan" }
            ]
            """);
        await fixture.RunAsync();

        Assert.Equal(["JO", "SY"], await CodesAsync(fixture));
    }

    [Fact]
    public async Task OnInsertedAsync_links_new_rows_to_each_other_and_to_existing_rows()
    {
        await using var fixture = await CreateAsync();
        fixture.Files.Set(StagesSeeder.File, """
            [
              { "Code": "FIRST", "Name": "First instance", "NextStageCode": "APPEAL" },
              { "Code": "APPEAL", "Name": "Appeal" }
            ]
            """);
        await fixture.RunAsync();

        fixture.Files.Set(StagesSeeder.File, """
            [
              { "Code": "FIRST", "Name": "First instance", "NextStageCode": "APPEAL" },
              { "Code": "APPEAL", "Name": "Appeal", "NextStageCode": "CASSATION" },
              { "Code": "CASSATION", "Name": "Cassation" },
              { "Code": "SUMMARY", "Name": "Summary", "NextStageCode": "APPEAL" }
            ]
            """);
        await fixture.RunAsync();

        var links = await fixture.QueryAsync(db => db.Stages.Select(x => new { x.Code, Next = x.NextStage!.Code }).ToDictionaryAsync(x => x.Code!, x => x.Next));
        Assert.Equal("APPEAL", links["FIRST"]);
        Assert.Equal("APPEAL", links["SUMMARY"]);
        Assert.Null(links["CASSATION"]);
        // APPEAL existed before CASSATION was added: existing rows are never changed.
        Assert.Null(links["APPEAL"]);
    }
}
