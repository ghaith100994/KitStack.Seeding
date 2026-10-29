namespace KitStack.Seeding;

/// <summary>A seeder failed and the failure policy is <see cref="SeedFailurePolicy.Throw"/>.</summary>
public class SeedingException : Exception
{
    /// <summary>Creates the exception.</summary>
    public SeedingException()
    {
    }

    /// <summary>Creates the exception.</summary>
    public SeedingException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    public SeedingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception for a failed seeder.</summary>
    public SeedingException(string seederName, string message, Exception innerException)
        : base(message, innerException)
    {
        SeederName = seederName;
    }

    /// <summary>The name of the seeder that failed.</summary>
    public string? SeederName { get; }
}
