# KitStack.Seeding.Tests

xUnit tests for all three packages. EF Core tests run against SQLite in-memory (one private connection per test)
with seed files held in an `InMemorySeedFileSource`, so they need no database server.

| Folder | Covers |
|---|---|
| `Core/` | File sources (content folder, embedded resource, in-memory, composite, path escapes), JSON settings, `SeedDates`, the runner (order, failure policies, disabled seeders, cancellation, per-context options) and DI scanning. |
| `EntityFrameworkCore/` | `JsonSeeder` (insert while empty, soft-deleted tables, failures and change-tracker clean-up), `LookupSeeder` (additive runs, no overwrites, soft deletes, keys, `IsActive`, `ConflictsWith`, `OnInsertedAsync` links), `ResolvingSeeder` (references by key, prerequisites, soft-deleted references) and database initialization, including the `UseSeeding` bridge. |
| `Support/` | The test model (with soft-delete query filters), sample seeders and `SeedingFixture`. |

```bash
dotnet test
```

Not covered yet: SQL Server through Testcontainers (named query filters in EF Core 10 are worth a check there).
