namespace KitStack.Seeding;

/// <summary>
/// Dates for demo seed files written as day offsets from the day the seed runs (<c>"RegisteredInDays": -237</c>), so
/// demo data always looks recent.
/// </summary>
public static class SeedDates
{
    /// <summary>Today plus <paramref name="days"/> (negative for the past).</summary>
    public static DateOnly Day(int days, TimeProvider? clock = null) => Today(clock).AddDays(days);

    /// <summary>Today plus <paramref name="days"/>, or <see langword="null"/> when <paramref name="days"/> is <see langword="null"/>.</summary>
    public static DateOnly? Day(int? days, TimeProvider? clock = null) => days is { } d ? Day(d, clock) : null;

    /// <summary>
    /// Today plus <paramref name="days"/> at <paramref name="time"/> (local time), or <see langword="null"/> when
    /// <paramref name="days"/> is <see langword="null"/>.
    /// </summary>
    public static DateTime? At(int? days, TimeOnly time, TimeProvider? clock = null)
        => days is { } d ? Day(d, clock).ToDateTime(time) : null;

    private static DateOnly Today(TimeProvider? clock)
        => DateOnly.FromDateTime((clock ?? TimeProvider.System).GetLocalNow().DateTime);
}
