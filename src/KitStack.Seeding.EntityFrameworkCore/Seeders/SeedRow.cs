using System.Text.Json;

namespace KitStack.Seeding.EntityFrameworkCore;

/// <summary>An entity inserted by a <see cref="LookupSeeder{TContext, TEntity}"/>, with the file row it was read from.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <param name="Entity">The inserted entity (tracked; its generated keys are set).</param>
/// <param name="Row">The JSON row, for values that are not entity properties (e.g. <c>"NextStageCode"</c>).</param>
public sealed record SeedRow<TEntity>(TEntity Entity, JsonElement Row)
{
    /// <summary>A string property of <see cref="Row"/> (case-insensitive name), or <see langword="null"/>.</summary>
    public string? GetString(string name)
        => SeedJson.TryGetProperty(Row, name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
