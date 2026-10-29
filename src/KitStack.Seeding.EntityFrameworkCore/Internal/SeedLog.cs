using Microsoft.Extensions.Logging;

namespace KitStack.Seeding.EntityFrameworkCore;

internal static partial class SeedLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "{Seeder}: the table already has data; nothing to seed.")]
    public static partial void AlreadySeeded(ILogger logger, string seeder);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Seeder}: no records found in {File}.")]
    public static partial void EmptyFile(ILogger logger, string seeder, string file);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Seeder}: {Count} records seeded from {File}.")]
    public static partial void Seeded(ILogger logger, string seeder, int count, string file);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{Seeder}: all rows of {File} already exist.")]
    public static partial void NothingNew(ILogger logger, string seeder, string file);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Seeder}: a row of {File} has no key and was skipped.")]
    public static partial void MissingKey(ILogger logger, string seeder, string file);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Seeder}: key '{Key}' appears more than once in {File}; the first row was used.")]
    public static partial void DuplicateKey(ILogger logger, string seeder, string key, string file);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Seeder}: row '{Key}' of {File} conflicts with an existing row and was skipped.")]
    public static partial void Conflict(ILogger logger, string seeder, string key, string file);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Seeder}: prerequisites are not in place yet; will try again at the next run.")]
    public static partial void NotReady(ILogger logger, string seeder);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Seeder}: {Key} skipped, {Reason}.")]
    public static partial void Skipped(ILogger logger, string seeder, string? key, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying {Count} pending migrations to {Context}.")]
    public static partial void Migrating(ILogger logger, int count, string context);
}
