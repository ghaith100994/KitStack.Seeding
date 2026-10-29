namespace KitStack.Seeding;

/// <summary>
/// An entity with an active flag. Lookup seeders set <see cref="IsActive"/> from each file row: a row is active unless
/// it says <c>"IsActive": false</c>. This works even when the property is <c>[JsonIgnore]</c> on the entity.
/// </summary>
public interface ISeedActivatable
{
    /// <summary>Whether the row is active.</summary>
    bool IsActive { get; set; }
}
