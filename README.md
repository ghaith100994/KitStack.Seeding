# KitStack.Seeding

[![CI](https://github.com/ghaith100994/KitStack.Seeding/actions/workflows/ci.yml/badge.svg)](https://github.com/ghaith100994/KitStack.Seeding/actions/workflows/ci.yml)
[![License: Apache-2.0](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](./LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)

KitStack.Seeding seeds reference data, lookup data and demo data from versioned JSON files into EF Core, in a fixed
order, once per start, safely. It is the generic part of the seeding code that every module of the InnovatePro ERP
copied (`*JsonSeederBase`, `*LookupSeederBase`, `*DemoSeederBase`, `*SeederRunner`, `*DbInitializer`, `I*Seeder`),
extracted into one library.

EF Core's `HasData` and `UseSeeding` do not cover what line-of-business apps need from seed files:

- **Additive lookups.** A new release appends rows to `Countries.json`; existing databases get the new rows at the
  next start.
- **No overwrites.** Rows that already exist are never changed, so administrators' edits survive.
- **Soft deletes are respected.** A row an administrator deleted is not brought back.
- **Foreign keys by natural key.** Seed rows say `"CountryCode": "SY"`, not `"CountryId": 42`.
- **Ordering and failure policy.** Seeders run in a declared order; a failure either stops startup or is logged
  and skipped.

## Packages

| Package | Contents |
|---|---|
| `KitStack.Seeding.Abstractions` | `ISeeder<TContext>`, `ISeederRunner<TContext>`, `ISeedFileSource`, `ISeedActivatable`, `SeedFailurePolicy`, `SeedingResult`. No dependencies. |
| `KitStack.Seeding` | `SeederRunner<TContext>`, file sources (content folder, embedded resources, in-memory, composite), `SeedJson` settings, `SeedDates`, `AddSeeding<TContext>()` with assembly scanning. No EF Core dependency. |
| `KitStack.Seeding.EntityFrameworkCore` | `JsonSeeder`, `LookupSeeder`, `ResolvingSeeder`, `SeederBase`, `DatabaseInitializer` (migrate, then seed), `InitializeDatabaseAsync<TContext>()` and the `UseKitStackSeeding` bridge to EF Core's `UseSeeding`. |

Applications normally reference only `KitStack.Seeding.EntityFrameworkCore`; it brings the other two.

## Quick start

```csharp
// Program.cs
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));

builder.Services.AddSeeding<AppDbContext>(o =>
    {
        o.FailurePolicy = SeedFailurePolicy.Throw;   // default; LogAndContinue logs and runs the next seeder
        o.Files.FromContentFolder("Seeding/Files");  // default when no source is configured
    })
    .AddSeedersFromAssemblyContaining<AppDbContext>();

var app = builder.Build();
await app.Services.InitializeDatabaseAsync<AppDbContext>();   // apply pending migrations, then seed
```

```xml
<!-- The project that holds the JSON files -->
<ItemGroup>
  <None Update="Seeding\Files\*.json" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

```csharp
public static class AppSeedOrder
{
    public const int Countries = 1;      // lookups: no foreign keys
    public const int Currencies = 2;

    public const int SecondPhaseStart = 100;
    public const int Cities = SecondPhaseStart;   // rows that reference the lookups
}

public class CountriesSeeder(ISeedFileSource<AppDbContext> files, ILogger<CountriesSeeder> logger)
    : LookupSeeder<AppDbContext, Country>(files, logger)
{
    protected override string FileName => "01Countries.json";
    public override int Order => AppSeedOrder.Countries;
    protected override string? KeyOf(Country entity) => entity.Code;
}
```

```jsonc
// Seeding/Files/01Countries.json - comments, trailing commas, any property case and enum names are allowed
[
  { "Code": "SY", "NameEn": "Syria", "NameAr": "سوريا" },
  { "Code": "IQ", "NameEn": "Iraq", "NameAr": "العراق", "IsActive": false },
]
```

A runnable version is in [samples/KitStack.Seeding.Sample](samples/KitStack.Seeding.Sample).

## Choosing a seeder

| Base class | Runs when | What it writes | Use it for |
|---|---|---|---|
| `LookupSeeder<TContext, TEntity>` | Every run | Only file rows whose key (`KeyOf`) is not in the table yet. Soft-deleted rows count as present. | Lookups and reference data that grow between releases: codes, types, statuses, courts, channels. |
| `JsonSeeder<TContext, TEntity>` | While the table is empty | The whole file, once. | Data that is seeded once and then owned by the users. |
| `ResolvingSeeder<TContext, TEntity, TRow>` | While the table is empty and `IsReadyAsync` is true | One entity per file row, built by `ResolveAsync`, which looks up references by natural key. | Rows with foreign keys, aggregates with children, demo data. |
| `SeederBase<TContext>` | Every run | Whatever `SeedCoreAsync` does. | Hand-written seeders (e.g. number settings) that still get file reading and failure clean-up. |
| `ISeeder<TContext>` | Every run | Whatever `SeedAsync` does. | Seeders that need nothing from the library. |

Every seeder must be idempotent: it runs at every start.

### Lookup seeders

```csharp
public class StagesSeeder(ISeedFileSource<AppDbContext> files, ILogger<StagesSeeder> logger)
    : LookupSeeder<AppDbContext, CaseStage>(files, logger)
{
    protected override string FileName => "03CaseStages.json";
    public override int Order => AppSeedOrder.CaseStages;
    protected override string? KeyOf(CaseStage entity) => entity.Code;

    // A second unique value: skip a new row whose name is already used under another code.
    protected override bool ConflictsWith(CaseStage entity, IReadOnlyList<CaseStage> existing)
        => existing.Any(x => string.Equals(x.NameEn, entity.NameEn, StringComparison.OrdinalIgnoreCase));

    // Rows that point at each other by code: link them after the insert. Changes are saved for you.
    protected override async Task OnInsertedAsync(AppDbContext db, IReadOnlyList<SeedRow<CaseStage>> inserted, CancellationToken ct)
    {
        var byCode = await db.CaseStages.ToDictionaryAsync(x => x.Code!, StringComparer.OrdinalIgnoreCase, ct);
        foreach (var row in inserted)
            if (row.GetString("NextStageCode") is { } next && byCode.TryGetValue(next, out var stage))
                row.Entity.NextStageId = stage.Id;
    }
}
```

- Keys are trimmed and compared case-insensitively (override `KeyComparer` to change this). Rows without a key, and
  repeated keys within the file, are skipped with a warning.
- `ConflictsWith` receives the rows already in the database **and** the new rows accepted so far.
- If the entity implements `ISeedActivatable`, `IsActive` is read from every row: a row is active unless it says
  `"IsActive": false`. This works even when `IsActive` is `[JsonIgnore]` on the entity.
- `OnRow(entity, JsonElement row)` sets other values the entity does not bind from JSON.

### Resolving seeders

```csharp
public class CitiesSeeder(ISeedFileSource<AppDbContext> files, ILogger<CitiesSeeder> logger)
    : ResolvingSeeder<AppDbContext, City, CitiesSeeder.Row>(files, logger)
{
    protected override string FileName => "100Cities.json";
    public override int Order => AppSeedOrder.Cities;

    // Wait for a later start until the countries exist (e.g. seeded by another module).
    protected override Task<bool> IsReadyAsync(AppDbContext db, CancellationToken ct) => db.Countries.AnyAsync(ct);

    protected override async Task<City?> ResolveAsync(Row row, AppDbContext db, CancellationToken ct)
    {
        var country = await FindAsync<Country>(db, x => x.Code, row.CountryCode, ct);   // loaded once per run
        if (country is null)
        {
            Skip(row.Name, $"country {row.CountryCode} not found");   // logged as a warning
            return null;
        }

        return new City { Name = row.Name, CountryId = country.Id, RecordedOn = SeedDates.Day(row.RecordedInDays) };
    }

    public sealed class Row
    {
        public string? Name { get; set; }
        public string? CountryCode { get; set; }
        public int RecordedInDays { get; set; }   // demo dates as offsets from today
    }
}
```

`FindAsync` respects query filters, so a soft-deleted lookup is never referenced. Child rows can be added to the
returned entity's collections; any other entity can be added to the context directly. Everything is saved once
after the last row.

## Seed files

Seeders name a file; they never build a path. Each context has its own `ISeedFileSource<TContext>`, built from its
options, so two contexts in one application can read from different places. Sources are searched in order.

```csharp
services.AddSeeding<AppDbContext>(o => o.Files
    .FromContentFolder("Seeding/Files")                                  // relative to AppContext.BaseDirectory
    .FromEmbeddedResources(typeof(AppDbContext).Assembly, "MyApp.Seeding.Files")
    .Use(new InMemorySeedFileSource().Set("99Extra.json", "[]")));
```

- **Content folder**: a relative folder is resolved against `AppContext.BaseDirectory` (the output folder), not
  the current directory and not the library's own location. File names cannot escape the folder.
- **Embedded resources**: `<EmbeddedResource Include="Seeding\Files\*.json" />`. With a prefix the manifest name
  must match exactly; without one, any resource ending in `.{fileName}` matches (two matches are an error).
- **In memory**: for tests, or for data built at startup.

JSON is read with `SeedJson.Options`: case-insensitive names, trailing commas, comments and enum names
(`"Kind": "Appeal"`). Override `SerializerOptions` in a seeder (start from `SeedJson.CreateOptions()`) to add
converters. A missing file throws `SeedFileNotFoundException`, naming every place that was searched.

## Options

| Option | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | `false` skips every seeder of the context. |
| `FailurePolicy` | `Throw` | `Throw` stops the run with a `SeedingException` naming the seeder. `LogAndContinue` logs a critical error, records the failure in the `SeedingResult` and runs the next seeder. |
| `DisabledSeeders` | empty | Seeder names (case-insensitive) that must not run, e.g. demo seeders in production. |
| `Files` | `Seeding/Files` | The file sources, see above. |

```csharp
services.AddSeeding<AppDbContext>(o =>
{
    o.FailurePolicy = SeedFailurePolicy.LogAndContinue;
    if (!configuration.GetValue<bool>("Seeding:DemoData"))
        o.DisabledSeeders.UnionWith(["CitiesDemoSeeder", "CustomersDemoSeeder"]);
});
```

When a seeder built on `SeederBase` fails, the change tracker is cleared before the runner continues, so the next
seeder's `SaveChanges` does not save the failed seeder's half-added rows.

## Running the seeders

| Call | Does |
|---|---|
| `services.InitializeDatabaseAsync<TContext>(DatabaseCreation.Migrate)` | In a new scope: apply pending migrations (relational) or `EnsureCreated` (other providers), then seed. Returns the `SeedingResult`. |
| `services.InitializeDatabaseAsync<TContext>(DatabaseCreation.EnsureCreated)` | Create the schema from the model (no migrations), then seed. |
| `services.InitializeDatabaseAsync<TContext>(DatabaseCreation.None)` / `services.RunSeedersAsync<TContext>()` | Only seed. |
| `o.UseKitStackSeeding<TContext>(sp)` in `AddDbContext((sp, o) => ...)` | Run the seeders from EF Core's own seeding hook: `Migrate`, `EnsureCreated` and `dotnet ef database update`. |

Use either `InitializeDatabaseAsync` or `UseKitStackSeeding` for a context, not both. `DatabaseInitializer<TContext>`
is public, so an application that has its own `IDbInitializer` can wrap it.

The runner orders seeders by `Order`, then by `Name`. A convention that has worked well: 1–99 for lookups that have
no foreign keys, 100 and up (`SecondPhaseStart`) for data that references them. Ordering across contexts is not
handled: initialize the contexts one after the other.

## Testing your seeders

Use SQLite in-memory and an `InMemorySeedFileSource`, run the seeders twice, and assert the second run adds nothing:

```csharp
var files = new InMemorySeedFileSource().Set("01Countries.json", """[{ "Code": "SY" }]""");
services.AddSeeding<AppDbContext>(o => o.Files.Use(files)).AddSeeder<CountriesSeeder>();
```

This repository's own tests ([tests/KitStack.Seeding.Tests](tests/KitStack.Seeding.Tests)) show the pattern.

## Migrating the ERP modules

[docs/migrating-from-erp.md](docs/migrating-from-erp.md) maps each ERP seeding file (LegalAffairs and
PublicRelations are used as the examples) to the library, with the module shims and the small behaviour
differences.

## Building

```bash
dotnet build
dotnet test
dotnet run --project samples/KitStack.Seeding.Sample
```

Tagging `vX.Y.Z` runs [publish-nuget.yml](.github/workflows/publish-nuget.yml), which packs the three packages at
that version and pushes them to nuget.org when the `NUGET_API_KEY` secret is set.

## License

Apache-2.0. See [LICENSE](LICENSE).
