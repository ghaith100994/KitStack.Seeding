using KitStack.Seeding.EntityFrameworkCore;
using KitStack.Seeding.Tests.Scenarios.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KitStack.Seeding.Tests.Scenarios;

/// <summary>
/// The whole life of an application that ships seed files: first install, restarts, users changing the seeded data,
/// new releases, a broken release, production settings, and a second context. Each step builds a new service
/// provider over the same database files, like a real restart.
/// </summary>
public class ApplicationLifecycleScenarioTests
{
    [Fact]
    public async Task Install_restart_user_changes_and_new_release()
    {
        await using var app = new CatalogApplication();

        // ---- 1. First install of release 1: the migration is applied, then every seeder runs in order.
        app.Release(CatalogReleases.Release1);
        var install = new CapturingLoggerProvider();
        var result = await app.StartAsync(install);

        Assert.True(result.Succeeded);
        Assert.Equal(["CategoriesSeeder", "CurrenciesSeeder", "DemoProductsSeeder"], result.Seeders.Select(x => x.Name));
        Assert.Contains(install.Entries, x => x.Message.Contains("pending migrations", StringComparison.Ordinal));

        var afterInstall = await app.SnapshotAsync();
        Assert.Equal(["BOOK", "ELEC", "PHONE", "TOY"], afterInstall.Categories);
        Assert.Equal(["EUR", "USD"], afterInstall.Currencies);
        Assert.Equal(["Novel", "Phone X"], afterInstall.Products);   // "Mystery item" names an unknown category
        Assert.Equal(2, afterInstall.Tags);
        Assert.Contains(install.At(LogLevel.Warning), x => x.Message.Contains("Mystery item skipped", StringComparison.Ordinal));

        await app.CatalogAsync(async db =>
        {
            var categories = await db.Categories.ToDictionaryAsync(x => x.Code);
            Assert.Equal(categories["ELEC"].Id, categories["PHONE"].ParentId);   // parent linked by code
            Assert.False(categories["TOY"].IsActive);                             // "IsActive": false in the file
            Assert.True(categories["BOOK"].IsActive);                             // missing IsActive means active

            var phone = await db.Products.Include(x => x.Tags).SingleAsync(x => x.Name == "Phone X");
            Assert.Equal(categories["PHONE"].Id, phone.CategoryId);              // code matched case-insensitively
            Assert.Equal(DateOnly.FromDateTime(DateTime.Today).AddDays(-10), phone.ListedOn);
            Assert.Equal(["5g", "new"], phone.Tags.Select(x => x.Value).Order());
            return 0;
        });

        // ---- 2. Restart with the same release: nothing changes.
        Assert.True((await app.StartAsync()).Succeeded);
        Assert.Equal(afterInstall, await app.SnapshotAsync(), new SnapshotComparer());

        // ---- 3. Users work with the data: rename a category, soft-delete one, rename a currency, delete a product.
        await app.CatalogAsync(async db =>
        {
            await db.Categories.Where(x => x.Code == "BOOK").ExecuteUpdateAsync(s => s.SetProperty(x => x.Name, "Books & Magazines"));
            await db.Categories.Where(x => x.Code == "TOY").ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDeleted, true));
            await db.Currencies.Where(x => x.Code == "EUR").ExecuteUpdateAsync(s => s.SetProperty(x => x.Name, "Euro (EU)"));
            return await db.Products.Where(x => x.Name == "Novel").ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDeleted, true));
        });

        // ---- 4. Release 2 ships edited files.
        app.Release(CatalogReleases.Release2);
        var upgrade = new CapturingLoggerProvider();
        Assert.True((await app.StartAsync(upgrade)).Succeeded);

        var afterUpgrade = await app.SnapshotAsync();
        // Lookups are additive: LAPTOP, EBOOK and TABLET are new. BOOKS is added too: its name "books" would clash
        // with BOOK's, but the admin renamed BOOK. "laptop" repeats LAPTOP within the file and is skipped.
        Assert.Equal(["BOOK", "BOOKS", "EBOOK", "ELEC", "LAPTOP", "PHONE", "TABLET", "TOY"], afterUpgrade.Categories);
        // Currencies were seeded once and belong to the users: GBP is not added.
        Assert.Equal(["EUR", "USD"], afterUpgrade.Currencies);
        // Demo products only seed an empty table.
        Assert.Equal(["Novel", "Phone X"], afterUpgrade.Products);

        await app.CatalogAsync(async db =>
        {
            var categories = await db.Categories.IgnoreQueryFilters().ToDictionaryAsync(x => x.Code);
            Assert.Equal("Electronics", categories["ELEC"].Name);              // existing rows are never updated
            Assert.Equal("Books & Magazines", categories["BOOK"].Name);        // the admin's edit survives
            Assert.True(categories["TOY"].IsDeleted);                          // still deleted, not duplicated
            Assert.Equal(categories["ELEC"].Id, categories["LAPTOP"].ParentId); // new row linked to an existing one
            Assert.Equal(categories["BOOK"].Id, categories["EBOOK"].ParentId);
            Assert.Null(categories["TABLET"].ParentId);                        // unknown parent: added, not linked
            Assert.Equal("Laptops", categories["LAPTOP"].Name);                // first of the repeated keys wins
            Assert.Equal("Euro (EU)", await db.Currencies.Where(x => x.Code == "EUR").Select(x => x.Name).SingleAsync());
            return 0;
        });
        Assert.Contains(upgrade.At(LogLevel.Warning), x => x.Message.Contains("'LAPTOP' appears more than once", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(upgrade.At(LogLevel.Warning), x => x.Message.Contains("TABLET skipped, parent MISSING not found", StringComparison.Ordinal));
        Assert.Contains(upgrade.At(LogLevel.Information), x => x.Message.Contains("CategoriesSeeder: 4 records seeded", StringComparison.Ordinal));

        // ---- 5. Restart on release 2: idempotent again.
        await app.StartAsync();
        Assert.Equal(afterUpgrade, await app.SnapshotAsync(), new SnapshotComparer());
    }

    [Fact]
    public async Task A_broken_release_stops_startup_or_is_skipped_depending_on_the_policy()
    {
        await using var app = new CatalogApplication();
        app.Release(CatalogReleases.Release1);
        await app.StartAsync();
        var before = await app.SnapshotAsync();

        app.Release(CatalogReleases.BrokenCategories);

        // Default policy: startup fails and names the seeder and the file.
        var ex = await Assert.ThrowsAsync<SeedingException>(() => app.StartAsync());
        Assert.Equal("CategoriesSeeder", ex.SeederName);
        Assert.Contains(CategoriesSeeder.File, ex.InnerException!.Message, StringComparison.Ordinal);

        // LogAndContinue: the broken seeder is reported, the others still run, the application starts.
        var logs = new CapturingLoggerProvider();
        var result = await app.StartAsync(logs, o => o.FailurePolicy = SeedFailurePolicy.LogAndContinue);

        Assert.False(result.Succeeded);
        Assert.Equal("CategoriesSeeder", Assert.Single(result.Failures).Name);
        Assert.Equal([SeederStatus.Failed, SeederStatus.Succeeded, SeederStatus.Succeeded], result.Seeders.Select(x => x.Status));
        Assert.Contains(logs.At(LogLevel.Critical), x => x.Message.Contains("CategoriesSeeder failed", StringComparison.Ordinal));
        Assert.Equal(before, await app.SnapshotAsync(), new SnapshotComparer());

        // The fix ships: the next start catches up.
        app.Release(CatalogReleases.Release2);
        Assert.True((await app.StartAsync()).Succeeded);
        Assert.Contains("LAPTOP", (await app.SnapshotAsync()).Categories);
    }

    [Fact]
    public async Task A_missing_seed_file_is_reported_with_the_folder_it_was_looked_for_in()
    {
        await using var app = new CatalogApplication();
        app.Release(new Dictionary<string, string>(CatalogReleases.Release1.Where(x => x.Key != CurrenciesSeeder.File)));

        var result = await app.StartAsync(configure: o => o.FailurePolicy = SeedFailurePolicy.LogAndContinue);

        var failure = Assert.IsType<SeedFileNotFoundException>(Assert.Single(result.Failures).Exception);
        Assert.Equal(CurrenciesSeeder.File, failure.FileName);
        Assert.Contains(app.SeedFolder, failure.Message, StringComparison.Ordinal);
        // Demo products need currencies: they wait instead of failing.
        Assert.Empty((await app.SnapshotAsync()).Products);
    }

    [Fact]
    public async Task Production_settings_turn_demo_data_or_all_seeding_off()
    {
        await using var app = new CatalogApplication();
        app.Release(CatalogReleases.Release1);

        var result = await app.StartAsync(configure: o => o.DisabledSeeders.Add(nameof(DemoProductsSeeder)));

        Assert.Equal(SeederStatus.Disabled, result.Seeders.Single(x => x.Name == nameof(DemoProductsSeeder)).Status);
        var snapshot = await app.SnapshotAsync();
        Assert.Equal(4, snapshot.Categories.Count);
        Assert.Empty(snapshot.Products);

        await using var off = new CatalogApplication();
        off.Release(CatalogReleases.Release1);
        Assert.Empty((await off.StartAsync(configure: o => o.Enabled = false)).Seeders);
        Assert.Empty((await off.SnapshotAsync()).Categories);   // migrated, but nothing seeded
    }

    [Fact]
    public async Task Each_context_reads_its_own_files_and_options()
    {
        await using var app = new CatalogApplication();
        app.Release(CatalogReleases.Release1);

        // Disabling a seeder of the catalog context does not touch the reference context, which reads embedded files.
        await app.StartAsync(configure: o => o.DisabledSeeders.Add(nameof(CategoriesSeeder)));

        Assert.Empty((await app.SnapshotAsync()).Categories);
        Assert.Equal(["JO", "LB", "SY"], await app.ReferenceAsync(db => db.Countries.OrderBy(x => x.Code).Select(x => x.Code).ToListAsync()));
    }

    [Fact]
    public async Task EF_Core_migrations_seed_through_the_UseSeeding_bridge()
    {
        await using var app = new CatalogApplication();
        app.Release(CatalogReleases.Release1);

        await using (var services = app.BuildServices(new CapturingLoggerProvider(), seedFromEfCore: true))
        {
            await using var scope = services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
        }

        var snapshot = await app.SnapshotAsync();
        Assert.Equal(4, snapshot.Categories.Count);
        Assert.Equal(["Novel", "Phone X"], snapshot.Products);
    }

    private sealed class SnapshotComparer : IEqualityComparer<CatalogSnapshot>
    {
        public bool Equals(CatalogSnapshot? x, CatalogSnapshot? y)
            => x is not null && y is not null
               && x.Categories.SequenceEqual(y.Categories) && x.Currencies.SequenceEqual(y.Currencies)
               && x.Products.SequenceEqual(y.Products) && x.Tags == y.Tags;

        public int GetHashCode(CatalogSnapshot obj) => obj.Tags;
    }
}
