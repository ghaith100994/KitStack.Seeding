using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KitStack.Seeding.EntityFrameworkCore;

/// <summary>
/// Reads a JSON file of <typeparamref name="TEntity"/> and inserts it when the table is empty. Once the table has rows
/// the file is not read again, so later edits to the file only reach new databases (use
/// <see cref="LookupSeeder{TContext, TEntity}"/> for data that grows between releases).
/// <code>
/// public class CurrenciesSeeder(ISeedFileSource&lt;AppDbContext&gt; files, ILogger&lt;CurrenciesSeeder&gt; logger)
///     : JsonSeeder&lt;AppDbContext, Currency&gt;(files, logger)
/// {
///     protected override string FileName =&gt; "02Currencies.json";
///     public override int Order =&gt; AppSeedOrder.Currencies;
/// }
/// </code>
/// </summary>
/// <typeparam name="TContext">The context the seeder writes to.</typeparam>
/// <typeparam name="TEntity">The entity the file holds.</typeparam>
public abstract class JsonSeeder<TContext, TEntity>(ISeedFileSource<TContext> files, ILogger logger) : SeederBase<TContext>(files, logger)
    where TContext : DbContext
    where TEntity : class
{
    /// <summary>The file name, e.g. <c>"01Countries.json"</c>, looked up in the context's file sources.</summary>
    protected abstract string FileName { get; }

    /// <summary>
    /// Whether global query filters are ignored when checking for existing rows (default <see langword="true"/>): a
    /// table whose rows are all soft-deleted still counts as seeded.
    /// </summary>
    protected virtual bool IgnoreQueryFilters => true;

    /// <inheritdoc />
    protected override async Task SeedCoreAsync(TContext context, CancellationToken cancellationToken)
    {
        if (await HasDataAsync(context, cancellationToken).ConfigureAwait(false))
        {
            SeedLog.AlreadySeeded(Logger, Name);
            return;
        }

        var entities = await ReadAsync<TEntity>(FileName, cancellationToken).ConfigureAwait(false);
        if (entities.Count == 0)
        {
            SeedLog.EmptyFile(Logger, Name, FileName);
            return;
        }

        await OnBeforeSaveAsync(context, entities, cancellationToken).ConfigureAwait(false);
        context.Set<TEntity>().AddRange(entities);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        SeedLog.Seeded(Logger, Name, entities.Count, FileName);
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

    /// <summary>Called with the rows read from the file before they are added; change, add or remove rows here.</summary>
    protected virtual Task OnBeforeSaveAsync(TContext context, List<TEntity> entities, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
