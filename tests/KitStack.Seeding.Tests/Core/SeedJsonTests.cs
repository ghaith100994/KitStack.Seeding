using System.Text.Json;
using KitStack.Seeding;
using KitStack.Seeding.Tests.Support;

namespace KitStack.Seeding.Tests.Core;

public class SeedJsonTests
{
    [Fact]
    public async Task Reads_comments_trailing_commas_any_case_and_enum_names()
    {
        var source = new InMemorySeedFileSource().Set("c.json", """
            // countries
            [
              { "code": "SY", "NAME": "Syria", "Kind": "Republic", },
              /* a second row */
              { "Code": "JO", "Name": "Jordan", "kind": "kingdom" },
            ]
            """);

        var rows = await source.ReadListAsync<Country>("c.json");

        Assert.Equal(["SY", "JO"], rows.Select(x => x.Code));
        Assert.Equal([CountryKind.Republic, CountryKind.Kingdom], rows.Select(x => x.Kind));
    }

    [Fact]
    public async Task A_null_document_reads_as_an_empty_list()
    {
        var source = new InMemorySeedFileSource().Set("n.json", "null");

        Assert.Empty(await source.ReadListAsync<Country>("n.json"));
    }

    [Fact]
    public async Task Invalid_json_names_the_file()
    {
        var source = new InMemorySeedFileSource().Set("bad.json", "[{ \"Code\": }]");

        var ex = await Assert.ThrowsAsync<JsonException>(() => source.ReadListAsync<Country>("bad.json"));
        Assert.Contains("bad.json", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Options_are_read_only_and_CreateOptions_returns_a_writable_copy()
    {
        Assert.True(SeedJson.Options.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => SeedJson.Options.AllowTrailingCommas = false);

        var copy = SeedJson.CreateOptions();
        copy.AllowTrailingCommas = false;
        Assert.True(copy.PropertyNameCaseInsensitive);
    }

    [Theory]
    [InlineData("""{ "IsActive": false }""", false)]
    [InlineData("""{ "isactive": false }""", false)]
    [InlineData("""{ "IsActive": true }""", true)]
    [InlineData("""{ "IsActive": null }""", true)]
    [InlineData("""{ }""", true)]
    public void IsNotFalse_treats_only_an_explicit_false_as_inactive(string json, bool expected)
    {
        using var document = JsonDocument.Parse(json);

        Assert.Equal(expected, SeedJson.IsNotFalse(document.RootElement, "IsActive"));
    }

    [Fact]
    public void SeedDates_are_offsets_from_today()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 3, 10, 15, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 3, 8), SeedDates.Day(-2, clock));
        Assert.Null(SeedDates.Day(null, clock));
        Assert.Equal(new DateTime(2026, 3, 11, 10, 0, 0), SeedDates.At(1, new TimeOnly(10, 0), clock));
        Assert.Null(SeedDates.At(null, new TimeOnly(10, 0), clock));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
