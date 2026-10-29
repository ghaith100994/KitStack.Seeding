using KitStack.Seeding;

namespace KitStack.Seeding.Tests.Core;

public class ExceptionTests
{
    [Fact]
    public void SeedingException_keeps_the_seeder_name_and_cause()
    {
        var cause = new InvalidOperationException("cause");

        var ex = new SeedingException("CountriesSeeder", "failed", cause);

        Assert.Equal("CountriesSeeder", ex.SeederName);
        Assert.Same(cause, ex.InnerException);
        Assert.Null(new SeedingException("plain").SeederName);
        Assert.Same(cause, new SeedingException("wrapped", cause).InnerException);
        Assert.NotNull(new SeedingException().Message);
    }

    [Fact]
    public void SeedFileNotFoundException_is_a_FileNotFoundException_with_the_file_name()
    {
        FileNotFoundException ex = new SeedFileNotFoundException("not found", "01Countries.json");

        Assert.Equal("01Countries.json", ex.FileName);
        Assert.Equal("not found", new SeedFileNotFoundException("not found").Message);
        Assert.NotNull(new SeedFileNotFoundException("wrapped", new IOException()).InnerException);
        Assert.NotNull(new SeedFileNotFoundException().Message);
    }

    [Fact]
    public void An_empty_result_has_succeeded()
    {
        Assert.True(SeedingResult.Empty.Succeeded);
        Assert.Empty(SeedingResult.Empty.Failures);
    }
}
