# KitStack.Seeding

The runner, file sources and registration for [KitStack.Seeding](https://github.com/ghaith100994/KitStack.Seeding).
It has no EF Core dependency: `TContext` can be any class. For EF Core seeders reference
`KitStack.Seeding.EntityFrameworkCore`, which brings this package.

```csharp
services.AddLogging();
services.AddSeeding<AppDbContext>(o =>
    {
        o.FailurePolicy = SeedFailurePolicy.LogAndContinue;
        o.DisabledSeeders.Add("DemoCustomersSeeder");
        o.Files.FromContentFolder("Seeding/Files")
               .FromEmbeddedResources(typeof(AppDbContext).Assembly);
    })
    .AddSeedersFromAssemblyContaining<AppDbContext>();   // or .AddSeeder<CountriesSeeder>()

SeedingResult result = await provider.RunSeedersAsync<AppDbContext>();
```

## Contents

| Type | Purpose |
|---|---|
| `SeederRunner<TContext>` | Orders seeders by `Order` then `Name`, skips disabled ones, applies the failure policy. Rethrows cancellation. |
| `SeedingOptions` | `Enabled`, `FailurePolicy`, `DisabledSeeders`, `Files`. Kept per context (named options). |
| `ContentFolderSeedFileSource` | A folder; relative paths resolve against `AppContext.BaseDirectory`. Rejects names that leave the folder. |
| `EmbeddedResourceSeedFileSource` | Manifest resources, by exact prefix or by name suffix. |
| `InMemorySeedFileSource` | Files in memory, for tests. |
| `CompositeSeedFileSource` | Several sources, first match wins. |
| `SeedJson` | The shared serializer settings (case-insensitive, comments, trailing commas, enum names) and row helpers. |
| `SeedFileSourceExtensions` | `OpenReadAsync` (throws when missing) and `ReadListAsync<T>`. |
| `SeedDates` | Demo dates as day offsets from today. |
| `AddSeeding<TContext>()`, `SeedingBuilder<TContext>` | Registration, `AddSeeder`, `AddSeedersFromAssembly`. Seeders are transient. |

Logging must be registered (`AddLogging()`); ASP.NET Core and generic hosts do this already.
