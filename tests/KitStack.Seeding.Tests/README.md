# KitStack.Seeding.Tests

xUnit tests for all three packages. EF Core tests run against SQLite in-memory (one private connection per test)
with seed files held in an `InMemorySeedFileSource`, so they need no database server.

| Folder | Covers |
|---|---|
| `Core/` | File sources (content folder, embedded resource, in-memory, composite, path escapes), JSON settings, `SeedDates`, the runner (order, failure policies, disabled seeders, cancellation, per-context options) and DI scanning. |
| `EntityFrameworkCore/` | `JsonSeeder` (insert while empty, soft-deleted tables, failures and change-tracker clean-up), `LookupSeeder` (additive runs, no overwrites, soft deletes, keys, `IsActive`, `ConflictsWith`, `OnInsertedAsync` links), `ResolvingSeeder` (references by key, prerequisites, soft-deleted references) and database initialization, including the `UseSeeding` bridge. |
| `Scenarios/` | End-to-end application scenario, described below. |
| `Support/` | The test model (with soft-delete query filters), sample seeders and `SeedingFixture`. |

## Application scenario

`Scenarios/ApplicationLifecycleScenarioTests.cs` runs the library the way an application does. It uses SQLite
database files on disk, a hand-written EF Core migration, seed files copied into a folder per "release", and
seeders found by assembly scanning. Every start builds a new service provider. The scenario covers:

1. **First install.** The pending migration is applied, then categories (lookup with parent links and `IsActive`),
   currencies (seeded once) and demo products (resolved by code, with tags and relative dates) are seeded in order.
   A demo row with an unknown category is skipped with a warning.
2. **Restart.** Nothing changes; the snapshot of every table's keys is identical.
3. **Users change data.** They rename a category and a currency, soft-delete a category and soft-delete a product.
4. **Release 2.** New categories are added and linked to existing parents. Edited, renamed and deleted rows are
   left alone. Repeated keys and unknown parents are logged. Currencies and demo products are not re-seeded.
5. **Broken release.** By default startup fails with a `SeedingException` naming the seeder and the file. With
   `LogAndContinue` the failure is reported, the other seeders run, and the fixed release catches up.
6. **Missing file.** The error names the folder that was searched, and dependent demo data waits.
7. **Production settings.** Demo seeders can be disabled, or all seeding turned off.
8. **Two contexts.** A second context reads embedded resources and has its own options.
9. **EF Core bridge.** `Database.MigrateAsync()` seeds through `UseKitStackSeeding`.

```bash
dotnet test
```

Not covered yet: SQL Server through Testcontainers (named query filters in EF Core 10 are worth a check there).
