namespace KitStack.Seeding.EntityFrameworkCore;

/// <summary>How <see cref="DatabaseInitializer{TContext}"/> prepares the database before seeding.</summary>
public enum DatabaseCreation
{
    /// <summary>Apply pending migrations (relational providers); other providers use <c>EnsureCreated</c>. Default.</summary>
    Migrate = 0,

    /// <summary>Create the database and schema from the model when it does not exist (<c>EnsureCreated</c>); no migrations.</summary>
    EnsureCreated = 1,

    /// <summary>Leave the database as it is and only seed.</summary>
    None = 2,
}
