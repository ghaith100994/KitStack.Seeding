namespace KitStack.Seeding;

/// <summary>No configured <see cref="ISeedFileSource"/> has the requested seed file.</summary>
public class SeedFileNotFoundException : FileNotFoundException
{
    /// <summary>Creates the exception.</summary>
    public SeedFileNotFoundException()
    {
    }

    /// <summary>Creates the exception.</summary>
    public SeedFileNotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    public SeedFileNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception for <paramref name="fileName"/>, naming where it was looked for.</summary>
    public SeedFileNotFoundException(string message, string fileName)
        : base(message, fileName)
    {
    }
}
