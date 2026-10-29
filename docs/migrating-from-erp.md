# Migrating the ERP modules to KitStack.Seeding

Every new-generation ERP module (Agenda, Archiving, CouncilSecretariat, Diwan, ExecutiveOffice, FMS,
LegalAffairs, PublicRelations) carries the same seeding code under its own name. This guide uses LegalAffairs; the
PublicRelations files are identical apart from the names. The JSON files, the concrete seeders and the seed-order
constants stay in the module. Everything generic moves to the library.

## What moves and what stays

| ERP file | Becomes |
|---|---|
| `Seeding/LegalAffairsJsonSeederBase.cs` | `JsonSeeder<LegalAffairsContext, T>` (a one-line shim, below) |
| `Seeding/LegalAffairsLookupSeederBase.cs` | `LookupSeeder<LegalAffairsContext, T>` (a one-line shim) |
| `Seeding/LegalAffairsDemoSeederBase.cs` | Stays as a module class, now deriving from `SeederBase<LegalAffairsContext>`; or move each demo seeder to `ResolvingSeeder` |
| `Persistence/Initialization/ILegalAffairsSeeder.cs` | `ISeeder<LegalAffairsContext>` (deleted) |
| `Persistence/Initialization/LegalAffairsSeederRunner.cs` | `SeederRunner<LegalAffairsContext>` (deleted) |
| `Persistence/Initialization/LegalAffairsDbInitializer.cs` | `DatabaseInitializer<LegalAffairsContext>` behind a 10-line adapter |
| `Seeding/LegalAffairsSeedOrder.cs` | Stays |
| `Seeding/Seeders/*.cs`, `Seeding/Files/*.json`, the csproj `CopyToOutputDirectory` items | Stay |

Member renames: `GetSeedOrder()` → `Order`, `GetSeederName()` → `Name`, `InitializeAsync(ct)` →
`SeedAsync(context, ct)` (`SeedCoreAsync` in `SeederBase`), `ReadSeedFileAsync<T>(file, ct)` → `ReadAsync<T>(file, ct)`.

## Registration

```csharp
// LegalAffairs InfrastructureModule.cs, replacing the runner, the initializer and the ITransientService scan of seeders
services.AddSeeding<LegalAffairsContext>(o => o.FailurePolicy = SeedFailurePolicy.LogAndContinue)   // today's behaviour
        .AddSeedersFromAssembly(typeof(LegalAffairsContext).Assembly);
services.AddScoped<IDbInitializer, KitStackDbInitializerAdapter<LegalAffairsContext>>();
```

```csharp
// BuildingBlocks: one adapter for all modules
public sealed class KitStackDbInitializerAdapter<TContext>(DatabaseInitializer<TContext> initializer) : IDbInitializer
    where TContext : DbContext
{
    public Task InitializeAsync(CancellationToken cancellationToken) => initializer.InitializeAsync(DatabaseCreation.Migrate, cancellationToken);
}
// and: services.AddScoped(typeof(DatabaseInitializer<>));
```

Seed files are read from `Seeding/Files` under `AppContext.BaseDirectory` by default. The ERP read them next to the
module assembly (`Assembly.GetExecutingAssembly().Location`); in the host these are the same folder, because the
module assemblies and their `Seeding/Files` are copied into the host's output.

## Shims

```csharp
public abstract class LegalAffairsJsonSeederBase<TEntity>(ISeedFileSource<LegalAffairsContext> files, ILogger logger)
    : JsonSeeder<LegalAffairsContext, TEntity>(files, logger)
    where TEntity : class
{
    // The ERP checked for existing rows with query filters applied. Keep that for an identical snapshot,
    // or drop this line to treat an all-soft-deleted table as already seeded.
    protected override bool IgnoreQueryFilters => false;
}

public abstract class LegalAffairsLookupSeederBase<TEntity>(ISeedFileSource<LegalAffairsContext> files, ILogger logger)
    : LookupSeeder<LegalAffairsContext, TEntity>(files, logger)
    where TEntity : ActivationEntity
{
    // ActivationEntity.IsActive is [JsonIgnore]: read it from the row, as the ERP base did.
    // (Alternatively let ActivationEntity implement ISeedActivatable and delete this override.)
    protected override void OnRow(TEntity entity, JsonElement row) => entity.IsActive = SeedJson.IsNotFalse(row, "IsActive");
}
```

A concrete lookup seeder only changes its constructor and its order member:

```csharp
public class LegalCourtsSeeder(ISeedFileSource<LegalAffairsContext> files, ILogger<LegalCourtsSeeder> logger)
    : LegalAffairsLookupSeederBase<LegalCourt>(files, logger)
{
    protected override string FileName => "01LegalCourts.json";
    public override int Order => LegalAffairsSeedOrder.LegalCourtsSeeder;
    protected override string? KeyOf(LegalCourt entity) => entity.Code;
}
```

## Hand-written seeders

**`LegalCaseStagesSeeder`** (stages linked to their next stage by code) becomes a lookup seeder with
`OnInsertedAsync`; the two saves and the "next stage may be new or existing" logic are handled by the base:

```csharp
public class LegalCaseStagesSeeder(ISeedFileSource<LegalAffairsContext> files, ILogger<LegalCaseStagesSeeder> logger)
    : LegalAffairsLookupSeederBase<LegalCaseStage>(files, logger)
{
    protected override string FileName => "03LegalCaseStages.json";
    public override int Order => LegalAffairsSeedOrder.LegalCaseStagesSeeder;
    protected override string? KeyOf(LegalCaseStage entity) => entity.Code;

    protected override async Task OnInsertedAsync(LegalAffairsContext db, IReadOnlyList<SeedRow<LegalCaseStage>> inserted, CancellationToken ct)
    {
        var byCode = await db.Set<LegalCaseStage>().IgnoreQueryFilters().Where(x => x.Code != null)
            .ToDictionaryAsync(x => x.Code!, StringComparer.OrdinalIgnoreCase, ct);
        foreach (var row in inserted)
        {
            if (row.GetString("NextStageCode")?.Trim() is not { Length: > 0 } next)
                continue;
            if (byCode.TryGetValue(next, out var stage) && stage.DeletedOn is null)
                row.Entity.NextStageId = stage.Id;
            else
                Skip(row.Entity.Code, $"next stage '{next}' not found");
        }
    }
}
```

**`LegalAffairsNumberSettingsSeeder`** writes to `CoreContext`. Keep it as a seeder of the module's context that
injects `CoreContext`, so it still runs in the module's order:

```csharp
public class LegalAffairsNumberSettingsSeeder(CoreContext core, ILogger<LegalAffairsNumberSettingsSeeder> logger) : ISeeder<LegalAffairsContext>
{
    public int Order => LegalAffairsSeedOrder.NumberSettingsSeeder;

    public async Task SeedAsync(LegalAffairsContext _, CancellationToken ct)
    {
        // body of the old InitializeAsync, without the try/catch: the failure policy handles errors now
    }
}
```

## Demo seeders

The demo seeders (`1xxLegal….json`) need the module options (`Seeding:DemoData`), the Org module's people, and
references to rows of other demo files. Two ways to move them:

1. **Smallest change.** Keep `LegalAffairsDemoSeederBase<TEntity>` and `DemoReferences` in the module, derive the base
   from `SeederBase<LegalAffairsContext>` and rename `InitializeAsync` to `SeedCoreAsync(LegalAffairsContext db, ct)`.
   Remove its `try/catch`. Read files with `ReadAsync<T>(FileName, ct)`.
2. **Use `ResolvingSeeder`.** It already runs only while the table is empty, waits through `IsReadyAsync`, reads the
   rows and saves once. Each demo seeder keeps its `ResolveAsync` body (the loop body of today's `SeedAsync`):

```csharp
public abstract class LegalAffairsDemoSeederBase<TEntity, TRow>(
    ISeedFileSource<LegalAffairsContext> files, IOptions<LegalAffairsModuleOptions> options, ILogger logger)
    : ResolvingSeeder<LegalAffairsContext, TEntity, TRow>(files, logger)
    where TEntity : class
{
    protected static readonly TimeOnly DefaultTime = new(10, 0);

    // Off unless Modules:LegalAffairs:Seeding:DemoData; then wait for the Org people.
    protected override async Task<bool> IsReadyAsync(LegalAffairsContext db, CancellationToken ct)
        => options.Value.Seeding.DemoData && await db.Set<SharedPerson>().AnyAsync(p => p.WorkEmail != null, ct);

    // IHasCode stands for whatever interface the module's lookups share that exposes Code.
    protected Task<T?> LookupAsync<T>(LegalAffairsContext db, string? code, CancellationToken ct) where T : class, IHasCode
        => FindAsync<T>(db, x => x.Code, code, ct);

    protected static DateOnly Day(int days) => SeedDates.Day(days);
    protected static DateOnly? Day(int? days) => SeedDates.Day(days);
    protected static DateTime? At(int? days) => SeedDates.At(days, DefaultTime);
}
```

Demo data can also be switched off without code: `o.DisabledSeeders.Add("LegalCasesDemoSeeder")`.

## Behaviour differences

| Area | ERP | Library | Same result |
|---|---|---|---|
| Failures | Each seeder catches, logs Critical and swallows | `FailurePolicy`, default `Throw` | Set `LogAndContinue` |
| After a failure | Half-added rows stay tracked and the next seeder's `SaveChanges` tries to save them again | `SeederBase` clears the change tracker | Better; no setting |
| `JsonSeeder` "table is empty" check | Query filters applied | Ignored by default | `IgnoreQueryFilters => false` (shim above) |
| Lookup key | Exact value, case-insensitive | Trimmed, case-insensitive | Same unless keys have spaces |
| Repeated key in one file | Both rows inserted (or a unique-index error) | First row inserted, warning logged | Better; no setting |
| `ConflictsWith` | Sees the existing rows | Sees the existing rows and the new rows accepted so far | Same unless the file conflicts with itself |
| File location | Next to the module assembly | `AppContext.BaseDirectory/Seeding/Files` | Same folder in the host |

## Checking the switch

As the extraction blueprint says: before switching a module, run its seeders into an empty database and snapshot
`table → row count + sorted natural keys`; switch; run again and compare. Then run the seeders a second time and
assert nothing was added, and soft-delete a lookup row, run again and assert it is not restored.
