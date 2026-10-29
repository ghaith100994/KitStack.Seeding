# KitStack.Seeding.Sample

A console app that seeds a SQLite database (`sample.db` in the output folder) with three seeders:

| Seeder | Base | File |
|---|---|---|
| `CountriesSeeder` | `LookupSeeder`: additive by `Code`; one row has `"IsActive": false` | `Seeding/Files/01Countries.json` |
| `CurrenciesSeeder` | `JsonSeeder`: once, while the table is empty | `Seeding/Files/02Currencies.json` |
| `CitiesSeeder` | `ResolvingSeeder`: each row names its country by code; one row is skipped because its country does not exist | `Seeding/Files/100Cities.json` |

```bash
dotnet run --project samples/KitStack.Seeding.Sample
dotnet run --project samples/KitStack.Seeding.Sample   # adds nothing
```

Add a country to `Seeding/Files/01Countries.json` and run again: only the new country is inserted. Delete
`bin/.../sample.db` to start over.
