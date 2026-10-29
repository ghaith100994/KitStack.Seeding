using System.Text.Json;

namespace KitStack.Seeding;

/// <summary>Reading helpers on <see cref="ISeedFileSource"/>.</summary>
public static class SeedFileSourceExtensions
{
    /// <summary>Opens <paramref name="fileName"/>, or throws <see cref="SeedFileNotFoundException"/> naming where it was looked for.</summary>
    public static async Task<Stream> OpenReadAsync(this ISeedFileSource source, string fileName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return await source.TryOpenReadAsync(fileName, cancellationToken).ConfigureAwait(false)
            ?? throw new SeedFileNotFoundException($"Seed file '{fileName}' was not found in {source.Description}.", fileName);
    }

    /// <summary>
    /// Reads <paramref name="fileName"/> as a JSON array of <typeparamref name="T"/> (a <c>null</c> document reads as an
    /// empty list). Uses <see cref="SeedJson.Options"/> unless <paramref name="options"/> is given.
    /// </summary>
    /// <exception cref="SeedFileNotFoundException">No source has the file.</exception>
    public static async Task<List<T>> ReadListAsync<T>(this ISeedFileSource source, string fileName, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
    {
        var stream = await source.OpenReadAsync(fileName, cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            try
            {
                var rows = await JsonSerializer.DeserializeAsync<List<T>>(stream, options ?? SeedJson.Options, cancellationToken).ConfigureAwait(false);
                return rows is null ? [] : [.. rows.Where(x => x is not null)];
            }
            catch (JsonException ex)
            {
                throw new JsonException($"Seed file '{fileName}' is not a valid JSON array of {typeof(T).Name}: {ex.Message}", ex.Path, ex.LineNumber, ex.BytePositionInLine, ex);
            }
        }
    }
}
