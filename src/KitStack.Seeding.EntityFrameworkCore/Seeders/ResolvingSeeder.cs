using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KitStack.Seeding.EntityFrameworkCore;

/// <summary>
/// Seeds rows that reference other data: each file row (<typeparamref name="TRow"/>, a plain DTO) names its references
/// by natural key (a lookup code, a person's e-mail, ...), and <see cref="ResolveAsync"/> turns it into an entity.
/// The seeder runs only while the table is empty, and waits for a later run while <see cref="IsReadyAsync"/> is
/// <see langword="false"/> (e.g. the people it references are seeded by another module).
/// <code>
/// public class CitiesSeeder(ISeedFileSource&lt;AppDbContext&gt; files, ILogger&lt;CitiesSeeder&gt; logger)
///     : ResolvingSeeder&lt;AppDbContext, City, CitiesSeeder.Row&gt;(files, logger)
/// {
///     protected override string FileName =&gt; "100Cities.json";
///     public override int Order =&gt; AppSeedOrder.Cities;
///
///     protected override async Task&lt;City?&gt; ResolveAsync(Row row, AppDbContext db, CancellationToken ct)
///     {
///         var country = await FindAsync&lt;Country&gt;(db, c =&gt; c.Code, row.CountryCode, ct);
///         if (country is null) { Skip(row.Name, $"country {row.CountryCode} not found"); return null; }
///         return new City { Name = row.Name, CountryId = country.Id };
///     }
///
///     public sealed class Row { public string? Name { get; set; } public string? CountryCode { get; set; } }
/// }
/// </code>
/// </summary>
/// <typeparam name="TContext">The context the seeder writes to.</typeparam>
/// <typeparam name="TEntity">The entity that is seeded (and whose table must be empty).</typeparam>
/// <typeparam name="TRow">The shape of a file row.</typeparam>
public abstract class ResolvingSeeder<TContext, TEntity, TRow>(ISeedFileSource<TContext> files, ILogger logger) : SeederBase<TContext>(files, logger)
    where TContext : DbContext
    where TEntity : class
{
    private readonly Dictionary<Type, object> _loaded = [];
    private readonly Dictionary<(Type, Delegate), object> _indexes = [];

    /// <summary>The file name, e.g. <c>"100Cities.json"</c>, looked up in the context's file sources.</summary>
    protected abstract string FileName { get; }

    /// <summary>
    /// Whether global query filters are ignored when checking for existing rows (default <see langword="true"/>): a
    /// table whose rows are all soft-deleted still counts as seeded.
    /// </summary>
    protected virtual bool IgnoreQueryFilters => true;

    /// <inheritdoc />
    protected override async Task SeedCoreAsync(TContext context, CancellationToken cancellationToken)
    {
        _loaded.Clear();
        _indexes.Clear();

        if (await HasDataAsync(context, cancellationToken).ConfigureAwait(false))
        {
            SeedLog.AlreadySeeded(Logger, Name);
            return;
        }

        if (!await IsReadyAsync(context, cancellationToken).ConfigureAwait(false))
        {
            SeedLog.NotReady(Logger, Name);
            return;
        }

        var rows = await ReadAsync<TRow>(FileName, cancellationToken).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            SeedLog.EmptyFile(Logger, Name, FileName);
            return;
        }

        var count = 0;
        foreach (var row in rows)
        {
            var entity = await ResolveAsync(row, context, cancellationToken).ConfigureAwait(false);
            if (entity is null)
                continue;

            context.Set<TEntity>().Add(entity);
            count++;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        SeedLog.Seeded(Logger, Name, count, FileName);
    }

    /// <summary>Whether the table already has rows; when it does, nothing is seeded.</summary>
    protected virtual Task<bool> HasDataAsync(TContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        IQueryable<TEntity> query = context.Set<TEntity>();
        if (IgnoreQueryFilters)
            query = query.IgnoreQueryFilters();
        return query.AnyAsync(cancellationToken);
    }

    /// <summary>
    /// Whether the data this seeder references exists. When <see langword="false"/>, nothing is seeded and the table
    /// stays empty, so the seeder tries again at the next run. Defaults to <see langword="true"/>.
    /// </summary>
    protected virtual Task<bool> IsReadyAsync(TContext context, CancellationToken cancellationToken) => Task.FromResult(true);

    /// <summary>
    /// Builds the entity for <paramref name="row"/>, or returns <see langword="null"/> to skip it (call
    /// <see cref="SeederBase{TContext}.Skip"/> to log why). Child entities can be added to the returned entity's
    /// collections; other entities can be added to <paramref name="context"/> directly. Everything is saved once,
    /// after the last row.
    /// </summary>
    protected abstract Task<TEntity?> ResolveAsync(TRow row, TContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Finds a <typeparamref name="T"/> by a natural key (trimmed, case-insensitive). The table is loaded once per run
    /// (respecting query filters, so soft-deleted rows are not referenced); the first row wins when keys repeat.
    /// Returns <see langword="null"/> for a missing or blank <paramref name="key"/>.
    /// </summary>
    protected async Task<T?> FindAsync<T>(TContext context, Func<T, string?> keySelector, string? key, CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(keySelector);
        if (string.IsNullOrWhiteSpace(key))
            return null;

        if (!_indexes.TryGetValue((typeof(T), keySelector), out var index))
        {
            if (!_loaded.TryGetValue(typeof(T), out var rows))
            {
                rows = await context.Set<T>().ToListAsync(cancellationToken).ConfigureAwait(false);
                _loaded[typeof(T)] = rows;
            }

            index = ((List<T>)rows)
                .Select(x => (Key: keySelector(x)?.Trim(), Item: x))
                .Where(x => !string.IsNullOrEmpty(x.Key))
                .GroupBy(x => x.Key!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Item, StringComparer.OrdinalIgnoreCase);
            _indexes[(typeof(T), keySelector)] = index;
        }

        return ((Dictionary<string, T>)index).GetValueOrDefault(key.Trim());
    }
}
