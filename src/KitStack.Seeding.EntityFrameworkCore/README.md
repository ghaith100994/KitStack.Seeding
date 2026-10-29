# KitStack.Seeding.EntityFrameworkCore

EF Core seeders for [KitStack.Seeding](https://github.com/ghaith100994/KitStack.Seeding).

```csharp
builder.Services.AddSeeding<AppDbContext>().AddSeedersFromAssemblyContaining<AppDbContext>();
// ...
await app.Services.InitializeDatabaseAsync<AppDbContext>();   // migrate, then seed
```

| Type | Behaviour |
|---|---|
| `LookupSeeder<TContext, TEntity>` | Every run, inserts the file rows whose natural key (`KeyOf`) is not in the table. Never updates. Soft-deleted rows count as present. Hooks: `ConflictsWith`, `OnRow`, `OnBeforeSaveAsync`, `OnInsertedAsync`. Reads `IsActive` per row for `ISeedActivatable` entities. |
| `JsonSeeder<TContext, TEntity>` | Inserts the whole file while the table is empty (soft-deleted rows count, unless `IgnoreQueryFilters` is overridden to `false`). Hook: `OnBeforeSaveAsync`. |
| `ResolvingSeeder<TContext, TEntity, TRow>` | While the table is empty and `IsReadyAsync` is true, turns each `TRow` into an entity with `ResolveAsync`. `FindAsync<T>(db, keySelector, key)` looks references up by natural key, loading each table once per run. `Skip(key, reason)` logs skipped rows. |
| `SeederBase<TContext>` | Base of the above, for hand-written seeders: `Files`, `Logger`, `ReadAsync<T>`, `Skip`, and change-tracker clean-up when the seeder fails. |
| `DatabaseInitializer<TContext>` | `InitializeAsync(DatabaseCreation)`: `Migrate` (pending migrations, or `EnsureCreated` for non-relational providers), `EnsureCreated` or `None`, then the seeders. |
| `InitializeDatabaseAsync<TContext>()` | Runs the initializer in a new scope. Uses a registered `DatabaseInitializer<TContext>` if there is one. |
| `UseKitStackSeeding<TContext>(sp)` | Hooks the seeders into EF Core's `UseSeeding`/`UseAsyncSeeding`, so `Migrate`, `EnsureCreated` and `dotnet ef database update` seed. Use instead of `InitializeDatabaseAsync`, not with it. |

Seeders take `ISeedFileSource<TContext>` and a logger in their constructor and receive the context in
`SeedAsync`, so the context being migrated is the one they write to.
