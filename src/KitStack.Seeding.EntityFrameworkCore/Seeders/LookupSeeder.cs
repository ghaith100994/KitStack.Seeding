using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KitStack.Seeding.EntityFrameworkCore;

/// <summary>
/// Seeds a lookup table from JSON <b>additively</b> by a natural key (usually a code):
/// <list type="bullet">
/// <item><description>a file row is inserted only when no row with its key exists, so rows added to the file in a later release reach databases seeded earlier;</description></item>
/// <item><description>existing rows are never changed, so administrators' edits survive;</description></item>
/// <item><description>global query filters are ignored when reading existing rows, so a row an administrator soft-deleted counts as present and is not brought back.</description></item>
/// </list>
/// Keys are compared case-insensitively after trimming. When <typeparamref name="TEntity"/> implements
/// <see cref="ISeedActivatable"/>, <c>IsActive</c> is read from each row (active unless <c>"IsActive": false</c>).
/// <code>
/// public class CountriesSeeder(ISeedFileSource&lt;AppDbContext&gt; files, ILogger&lt;CountriesSeeder&gt; logger)
///     : LookupSeeder&lt;AppDbContext, Country&gt;(files, logger)
/// {
///     protected override string FileName =&gt; "01Countries.json";
///     public override int Order =&gt; AppSeedOrder.Countries;
///     protected override string? KeyOf(Country entity) =&gt; entity.Code;
/// }
/// </code>
/// </summary>
/// <typeparam name="TContext">The context the seeder writes to.</typeparam>
/// <typeparam name="TEntity">The lookup entity.</typeparam>
public abstract class LookupSeeder<TContext, TEntity>(ISeedFileSource<TContext> files, ILogger logger) : JsonSeeder<TContext, TEntity>(files, logger)
    where TContext : DbContext
    where TEntity : class
{
    /// <summary>How keys are compared. Defaults to <see cref="StringComparer.OrdinalIgnoreCase"/>.</summary>
    protected virtual StringComparer KeyComparer => StringComparer.OrdinalIgnoreCase;

    /// <inheritdoc />
    protected override async Task SeedCoreAsync(TContext context, CancellationToken cancellationToken)
    {
        var rows = await ReadAsync<JsonElement>(FileName, cancellationToken).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            SeedLog.EmptyFile(Logger, Name, FileName);
            return;
        }

        // Deleted rows count as present, so a lookup an administrator removed stays removed.
        IQueryable<TEntity> query = context.Set<TEntity>().AsNoTracking();
        if (IgnoreQueryFilters)
            query = query.IgnoreQueryFilters();
        var existing = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        var keys = new HashSet<string>(existing.Select(NormalizedKeyOf).OfType<string>(), KeyComparer);
        var known = new List<TEntity>(existing);
        var accepted = new List<SeedRow<TEntity>>();
        var seenInFile = new HashSet<string>(KeyComparer);
        foreach (var row in rows)
        {
            var entity = row.Deserialize<TEntity>(SerializerOptions);
            if (entity is null)
                continue;

            if (entity is ISeedActivatable activatable)
                activatable.IsActive = SeedJson.IsNotFalse(row, nameof(ISeedActivatable.IsActive));
            OnRow(entity, row);

            var key = NormalizedKeyOf(entity);
            if (key is null)
            {
                SeedLog.MissingKey(Logger, Name, FileName);
                continue;
            }

            if (!seenInFile.Add(key))
            {
                SeedLog.DuplicateKey(Logger, Name, key, FileName);
                continue;
            }

            if (keys.Contains(key))
                continue;

            if (ConflictsWith(entity, known))
            {
                SeedLog.Conflict(Logger, Name, key, FileName);
                continue;
            }

            keys.Add(key);
            known.Add(entity);
            accepted.Add(new SeedRow<TEntity>(entity, row));
        }

        if (accepted.Count == 0)
        {
            SeedLog.NothingNew(Logger, Name, FileName);
            return;
        }

        var entities = accepted.ConvertAll(x => x.Entity);
        await OnBeforeSaveAsync(context, entities, cancellationToken).ConfigureAwait(false);
        context.Set<TEntity>().AddRange(entities);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await OnInsertedAsync(context, accepted, cancellationToken).ConfigureAwait(false);
        if (context.ChangeTracker.HasChanges())
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        SeedLog.Seeded(Logger, Name, entities.Count, FileName);
    }

    /// <summary>The natural key a file row is matched on (e.g. the code). Rows without a key are skipped.</summary>
    protected abstract string? KeyOf(TEntity entity);

    /// <summary>
    /// Whether a new row clashes on another unique value with <paramref name="existing"/> (the rows in the database
    /// plus the new rows accepted so far), e.g. a unique name under a different code. Conflicting rows are skipped.
    /// </summary>
    protected virtual bool ConflictsWith(TEntity entity, IReadOnlyList<TEntity> existing) => false;

    /// <summary>Called for each file row after it is deserialized, to set values the entity does not bind from JSON.</summary>
    protected virtual void OnRow(TEntity entity, JsonElement row)
    {
    }

    /// <summary>
    /// Called after the new rows are saved, with their file rows. Use it to link rows to each other by key (e.g. a
    /// stage's <c>"NextStageCode"</c>); changes made here are saved afterwards.
    /// </summary>
    protected virtual Task OnInsertedAsync(TContext context, IReadOnlyList<SeedRow<TEntity>> inserted, CancellationToken cancellationToken)
        => Task.CompletedTask;

    private string? NormalizedKeyOf(TEntity entity)
        => KeyOf(entity) is { } key && !string.IsNullOrWhiteSpace(key) ? key.Trim() : null;
}
