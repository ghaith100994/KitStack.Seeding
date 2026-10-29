using KitStack.Seeding;
using KitStack.Seeding.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace KitStack.Seeding.Tests.EntityFrameworkCore;

public class JsonSeederTests
{
    private static string Currencies(params string[] codes) => TestJson.Of(codes.Select(c => new { Code = c, Name = c + " name" }));

    [Fact]
    public async Task Inserts_the_file_when_the_table_is_empty()
    {
        await using var fixture = await SeedingFixture.CreateAsync(b => b.AddSeeder<CurrenciesSeeder>());
        fixture.Files.Set(CurrenciesSeeder.File, Currencies("usd", "eur"));

        await fixture.RunAsync();

        // OnBeforeSaveAsync upper-cased the codes.
        Assert.Equal(["EUR", "USD"], await fixture.QueryAsync(db => db.Currencies.Select(x => x.Code!).OrderBy(x => x).ToListAsync()));
    }

    [Fact]
    public async Task Does_nothing_once_the_table_has_rows()
    {
        await using var fixture = await SeedingFixture.CreateAsync(b => b.AddSeeder<CurrenciesSeeder>());
        fixture.Files.Set(CurrenciesSeeder.File, Currencies("USD"));
        await fixture.RunAsync();

        fixture.Files.Set(CurrenciesSeeder.File, Currencies("USD", "EUR"));
        await fixture.RunAsync();

        Assert.Equal(1, await fixture.QueryAsync(db => db.Currencies.CountAsync()));
    }

    [Fact]
    public async Task A_table_whose_rows_are_all_soft_deleted_counts_as_seeded()
    {
        await using var fixture = await SeedingFixture.CreateAsync(b => b.AddSeeder<CurrenciesSeeder>());
        fixture.Files.Set(CurrenciesSeeder.File, Currencies("USD"));
        await fixture.RunAsync();
        await fixture.ExecuteAsync(db => db.Currencies.ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDeleted, true)));

        await fixture.RunAsync();

        Assert.Equal(1, await fixture.QueryAsync(db => db.Currencies.IgnoreQueryFilters().CountAsync()));
    }

    [Fact]
    public async Task An_empty_file_seeds_nothing()
    {
        await using var fixture = await SeedingFixture.CreateAsync(b => b.AddSeeder<CurrenciesSeeder>());
        fixture.Files.Set(CurrenciesSeeder.File, "[]");

        var result = await fixture.RunAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(0, await fixture.QueryAsync(db => db.Currencies.CountAsync()));
    }

    [Fact]
    public async Task A_missing_file_fails_the_seeder()
    {
        await using var fixture = await SeedingFixture.CreateAsync(b => b.AddSeeder<CurrenciesSeeder>());

        var ex = await Assert.ThrowsAsync<SeedingException>(fixture.RunAsync);

        Assert.IsType<SeedFileNotFoundException>(ex.InnerException);
    }

    [Fact]
    public async Task A_failed_seeder_does_not_leave_tracked_rows_for_the_next_one()
    {
        await using var fixture = await SeedingFixture.CreateAsync(
            b => b.AddSeeder<BrokenSeeder>().AddSeeder<CurrenciesSeeder>(),
            o => o.FailurePolicy = SeedFailurePolicy.LogAndContinue);
        fixture.Files.Set(CurrenciesSeeder.File, Currencies("USD"));

        var result = await fixture.RunAsync();

        Assert.Equal("BrokenSeeder", Assert.Single(result.Failures).Name);
        Assert.Equal(["USD"], await fixture.QueryAsync(db => db.Currencies.Select(x => x.Code!).ToListAsync()));
    }

    [Fact]
    public async Task A_failed_save_is_rolled_back_out_of_the_change_tracker()
    {
        await using var fixture = await SeedingFixture.CreateAsync(
            b => b.AddSeeder<CurrenciesSeeder>().AddSeeder<CountriesSeeder>(),
            o => o.FailurePolicy = SeedFailurePolicy.LogAndContinue);
        // Code is required: the insert fails in the database.
        fixture.Files.Set(CurrenciesSeeder.File, """[{ "Name": "No code" }]""");
        fixture.Files.Set(CountriesSeeder.File, """[{ "Code": "SY", "Name": "Syria" }]""");

        var result = await fixture.RunAsync();

        Assert.Equal("CurrenciesSeeder", Assert.Single(result.Failures).Name);
        Assert.Equal(1, await fixture.QueryAsync(db => db.Countries.CountAsync()));
        Assert.Equal(0, await fixture.QueryAsync(db => db.Currencies.CountAsync()));
    }
}
