# KitStack.Seeding.Abstractions

Contracts for [KitStack.Seeding](https://github.com/ghaith100994/KitStack.Seeding). No dependencies, no EF Core.

| Type | Purpose |
|---|---|
| `ISeeder<TContext>` | One seeder: `Order`, `Name` (defaults to the type name), `SeedAsync(context, ct)`. Must be idempotent. |
| `ISeederRunner<TContext>` | Runs a context's seeders in order and returns a `SeedingResult`. |
| `ISeedFileSource` / `ISeedFileSource<TContext>` | Where seed files come from. Seeders take the per-context variant in their constructor. |
| `ISeedActivatable` | Entities with `IsActive`; lookup seeders read it from each file row. |
| `SeedFailurePolicy` | `Throw` (default) or `LogAndContinue`. |
| `SeedingResult`, `SeederOutcome`, `SeederStatus` | What happened to each seeder in a run. |
| `SeedingException`, `SeedFileNotFoundException` | Failures surfaced by the runner and the file sources. |

Reference this package from projects that declare seeders but should not depend on the runner, such as a domain
project whose entities implement `ISeedActivatable`.
