using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KitStack.Seeding.EntityFrameworkCore;

/// <summary>
/// Base for EF Core seeders, including hand-written ones. When <see cref="SeedCoreAsync"/> throws, the change tracker
/// is cleared before the exception reaches the runner, so with <see cref="SeedFailurePolicy.LogAndContinue"/> the next
/// seeder does not save this seeder's half-added rows.
/// </summary>
/// <typeparam name="TContext">The context the seeder writes to.</typeparam>
public abstract class SeederBase<TContext> : ISeeder<TContext>
    where TContext : DbContext
{
    /// <summary>Creates the seeder.</summary>
    /// <param name="files">The context's seed files.</param>
    /// <param name="logger">The seeder's logger.</param>
    protected SeederBase(ISeedFileSource<TContext> files, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);
        Files = files;
        Logger = logger;
    }

    /// <summary>The context's seed files.</summary>
    protected ISeedFileSource<TContext> Files { get; }

    /// <summary>The seeder's logger.</summary>
    protected ILogger Logger { get; }

    /// <inheritdoc />
    public abstract int Order { get; }

    /// <inheritdoc />
    public virtual string Name => GetType().Name;

    /// <summary>The serializer settings for this seeder's files. Defaults to <see cref="SeedJson.Options"/>.</summary>
    protected virtual JsonSerializerOptions SerializerOptions => SeedJson.Options;

    /// <inheritdoc />
    public async Task SeedAsync(TContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            await SeedCoreAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            context.ChangeTracker.Clear();
            throw;
        }
    }

    /// <summary>Does the seeding.</summary>
    protected abstract Task SeedCoreAsync(TContext context, CancellationToken cancellationToken);

    /// <summary>Reads <paramref name="fileName"/> as a JSON array of <typeparamref name="T"/> with <see cref="SerializerOptions"/>.</summary>
    /// <exception cref="SeedFileNotFoundException">No configured source has the file.</exception>
    protected Task<List<T>> ReadAsync<T>(string fileName, CancellationToken cancellationToken)
        => Files.ReadListAsync<T>(fileName, SerializerOptions, cancellationToken);

    /// <summary>Logs that a row was skipped (warning), e.g. because a reference was not found.</summary>
    protected void Skip(string? key, string reason) => SeedLog.Skipped(Logger, Name, key, reason);
}
