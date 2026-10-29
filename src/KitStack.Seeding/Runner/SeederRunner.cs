using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KitStack.Seeding;

/// <summary>
/// Runs the registered seeders of <typeparamref name="TContext"/> one after the other, ordered by
/// <see cref="ISeeder{TContext}.Order"/> and then by <see cref="ISeeder{TContext}.Name"/>, applying the context's
/// <see cref="SeedingOptions"/>.
/// </summary>
public partial class SeederRunner<TContext>(
    IEnumerable<ISeeder<TContext>> seeders,
    IOptionsMonitor<SeedingOptions> options,
    ILogger<SeederRunner<TContext>> logger) : ISeederRunner<TContext>
    where TContext : class
{
    /// <inheritdoc />
    public virtual async Task<SeedingResult> RunAsync(TContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var settings = options.Get(SeedingOptions.NameFor<TContext>());
        var contextName = typeof(TContext).Name;
        if (!settings.Enabled)
        {
            LogDisabled(logger, contextName);
            return SeedingResult.Empty;
        }

        var ordered = seeders.OrderBy(x => x.Order).ThenBy(x => x.Name, StringComparer.Ordinal).ToList();
        LogStarting(logger, ordered.Count, contextName);

        var outcomes = new List<SeederOutcome>(ordered.Count);
        foreach (var seeder in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = seeder.Name;
            if (settings.DisabledSeeders.Contains(name))
            {
                LogSeederDisabled(logger, name);
                outcomes.Add(new SeederOutcome(name, seeder.Order, SeederStatus.Disabled, TimeSpan.Zero));
                continue;
            }

            var started = Stopwatch.GetTimestamp();
            try
            {
                LogSeederStarting(logger, name, seeder.Order);
                await seeder.SeedAsync(context, cancellationToken).ConfigureAwait(false);
                var elapsed = Stopwatch.GetElapsedTime(started);
                LogSeederDone(logger, name, elapsed.TotalMilliseconds);
                outcomes.Add(new SeederOutcome(name, seeder.Order, SeederStatus.Succeeded, elapsed));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
#pragma warning disable CA1031 // A seeder can fail in any way; the policy decides what happens.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                var elapsed = Stopwatch.GetElapsedTime(started);
                if (settings.FailurePolicy == SeedFailurePolicy.Throw)
                {
                    LogSeederFailedStopping(logger, ex, name);
                    throw new SeedingException(name, $"Seeder '{name}' of {contextName} failed: {ex.Message}", ex);
                }

                LogSeederFailed(logger, ex, name);
                outcomes.Add(new SeederOutcome(name, seeder.Order, SeederStatus.Failed, elapsed, ex));
            }
        }

        return new SeedingResult(outcomes);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeding of {Context} is disabled.")]
    private static partial void LogDisabled(ILogger logger, string context);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Running {Count} seeders for {Context}.")]
    private static partial void LogStarting(ILogger logger, int count, string context);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeder {Seeder} is disabled.")]
    private static partial void LogSeederDisabled(ILogger logger, string seeder);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Seeder {Seeder} (order {Order}) started.")]
    private static partial void LogSeederStarting(ILogger logger, string seeder, int order);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Seeder {Seeder} done in {ElapsedMs:0} ms.")]
    private static partial void LogSeederDone(ILogger logger, string seeder, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Seeder {Seeder} failed; continuing with the next seeder.")]
    private static partial void LogSeederFailed(ILogger logger, Exception exception, string seeder);

    [LoggerMessage(Level = LogLevel.Error, Message = "Seeder {Seeder} failed; stopping the run.")]
    private static partial void LogSeederFailedStopping(ILogger logger, Exception exception, string seeder);
}
