using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitStack.Seeding;

/// <summary>JSON settings and helpers shared by every seeder.</summary>
public static class SeedJson
{
    /// <summary>
    /// The serializer settings for seed files: case-insensitive property names, trailing commas, <c>//</c> and
    /// <c>/* */</c> comments, and enums written as their names (<c>"Kind": "Appeal"</c>). Read-only.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    /// <summary>A new, writable copy of <see cref="Options"/> to customise (e.g. to add converters).</summary>
    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    /// <summary>Finds a property of a JSON object by name, ignoring case.</summary>
    public static bool TryGetProperty(JsonElement row, string name, out JsonElement value)
    {
        if (row.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in row.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Reads an <c>IsActive</c>-style flag: <see langword="true"/> unless the row says <c>"name": false</c>
    /// (a missing property, <c>null</c> or anything else counts as active).
    /// </summary>
    public static bool IsNotFalse(JsonElement row, string name)
        => !(TryGetProperty(row, name, out var value) && value.ValueKind == JsonValueKind.False);
}
