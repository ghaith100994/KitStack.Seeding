# Changelog

## 0.1.0

First release, extracted from the seeding code repeated in every InnovatePro ERP module.

### Added

- `KitStack.Seeding.Abstractions`: `ISeeder<TContext>`, `ISeederRunner<TContext>`, `ISeedFileSource` /
  `ISeedFileSource<TContext>`, `ISeedActivatable`, `SeedFailurePolicy`, `SeedingResult`, `SeedingException`,
  `SeedFileNotFoundException`.
- `KitStack.Seeding`: `SeederRunner<TContext>` (ordered by `Order` then `Name`, failure policies, disabled seeders),
  `ContentFolderSeedFileSource`, `EmbeddedResourceSeedFileSource`, `InMemorySeedFileSource`,
  `CompositeSeedFileSource`, `SeedJson`, `SeedDates`, `AddSeeding<TContext>()` with `AddSeeder`,
  `AddSeedersFromAssembly` and `RunSeedersAsync<TContext>()`.
- `KitStack.Seeding.EntityFrameworkCore`: `SeederBase`, `JsonSeeder` (insert while empty), `LookupSeeder`
  (additive by natural key, soft-delete aware, `ConflictsWith`, `OnRow`, `OnInsertedAsync`, `ISeedActivatable`),
  `ResolvingSeeder` (references by natural key, `IsReadyAsync`, cached `FindAsync`), `DatabaseInitializer`,
  `InitializeDatabaseAsync<TContext>()` and `UseKitStackSeeding<TContext>()`.
