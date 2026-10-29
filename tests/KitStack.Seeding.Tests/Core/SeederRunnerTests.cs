using KitStack.Seeding;
using Microsoft.Extensions.DependencyInjection;

namespace KitStack.Seeding.Tests.Core;

public class SeederRunnerTests
{
    public sealed class Ctx
    {
        public List<string> Log { get; } = [];
    }

    private abstract class Recording(int order) : ISeeder<Ctx>
    {
        public int Order => order;

        public virtual Task SeedAsync(Ctx context, CancellationToken cancellationToken)
        {
            context.Log.Add(((ISeeder<Ctx>)this).Name);
            return Task.CompletedTask;
        }
    }

    private sealed class Third() : Recording(30);

    private sealed class FirstB() : Recording(10);

    private sealed class FirstA() : Recording(10);

    private sealed class Second() : Recording(20);

    private sealed class Failing() : Recording(15)
    {
        public override Task SeedAsync(Ctx context, CancellationToken cancellationToken) => throw new InvalidOperationException("boom");
    }

    private sealed class Named() : Recording(40), ISeeder<Ctx>
    {
        string ISeeder<Ctx>.Name => "Custom";
    }

    private static ServiceProvider Build(Action<SeedingBuilder<Ctx>> seeders, Action<SeedingOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<Ctx>();
        seeders(services.AddSeeding<Ctx>(configure));
        return services.BuildServiceProvider();
    }

    private static async Task<(SeedingResult Result, Ctx Context)> RunAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<Ctx>();
        var result = await scope.ServiceProvider.GetRequiredService<ISeederRunner<Ctx>>().RunAsync(context);
        return (result, context);
    }

    [Fact]
    public async Task Runs_by_order_then_by_name()
    {
        await using var provider = Build(b => b.AddSeeder<Third>().AddSeeder<FirstB>().AddSeeder<Second>().AddSeeder<FirstA>());

        var (result, context) = await RunAsync(provider);

        Assert.Equal(["FirstA", "FirstB", "Second", "Third"], context.Log);
        Assert.Equal(["FirstA", "FirstB", "Second", "Third"], result.Seeders.Select(x => x.Name));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Throw_policy_stops_the_run_and_names_the_seeder()
    {
        await using var provider = Build(b => b.AddSeeder<FirstA>().AddSeeder<Failing>().AddSeeder<Second>());

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<Ctx>();
        var ex = await Assert.ThrowsAsync<SeedingException>(() => scope.ServiceProvider.GetRequiredService<ISeederRunner<Ctx>>().RunAsync(context));

        Assert.Equal("Failing", ex.SeederName);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Equal(["FirstA"], context.Log);
    }

    [Fact]
    public async Task LogAndContinue_records_the_failure_and_runs_the_rest()
    {
        await using var provider = Build(
            b => b.AddSeeder<FirstA>().AddSeeder<Failing>().AddSeeder<Second>(),
            o => o.FailurePolicy = SeedFailurePolicy.LogAndContinue);

        var (result, context) = await RunAsync(provider);

        Assert.Equal(["FirstA", "Second"], context.Log);
        Assert.False(result.Succeeded);
        var failure = Assert.Single(result.Failures);
        Assert.Equal("Failing", failure.Name);
        Assert.Equal("boom", failure.Exception!.Message);
    }

    [Fact]
    public async Task Disabled_seeders_are_skipped_by_name()
    {
        await using var provider = Build(b => b.AddSeeder<FirstA>().AddSeeder<Second>(), o => o.DisabledSeeders.Add("second"));

        var (result, context) = await RunAsync(provider);

        Assert.Equal(["FirstA"], context.Log);
        Assert.Equal(SeederStatus.Disabled, result.Seeders.Single(x => x.Name == "Second").Status);
    }

    [Fact]
    public async Task Disabled_seeding_runs_nothing()
    {
        await using var provider = Build(b => b.AddSeeder<FirstA>(), o => o.Enabled = false);

        var (result, context) = await RunAsync(provider);

        Assert.Empty(context.Log);
        Assert.Empty(result.Seeders);
    }

    [Fact]
    public async Task Cancellation_is_not_treated_as_a_failure()
    {
        await using var provider = Build(b => b.AddSeeder<FirstA>(), o => o.FailurePolicy = SeedFailurePolicy.LogAndContinue);
        await using var scope = provider.CreateAsyncScope();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => scope.ServiceProvider.GetRequiredService<ISeederRunner<Ctx>>().RunAsync(new Ctx(), cts.Token));
    }

    [Fact]
    public async Task Default_name_is_the_type_name_and_can_be_overridden()
    {
        await using var provider = Build(b => b.AddSeeder<Named>());

        var (result, context) = await RunAsync(provider);

        Assert.Equal("Custom", result.Seeders.Single().Name);
        Assert.Equal(["Custom"], context.Log);
    }

    [Fact]
    public async Task Assembly_scanning_registers_concrete_seeders_once()
    {
        await using var provider = Build(b => b.AddSeedersFromAssemblyContaining<SeederRunnerTests>().AddSeedersFromAssemblyContaining<SeederRunnerTests>());

        var seeders = provider.GetServices<ISeeder<Ctx>>().Select(x => x.GetType().Name).Order().ToList();

        Assert.Equal(["Failing", "FirstA", "FirstB", "Named", "Second", "Third"], seeders);
    }

    [Fact]
    public void AddSeeder_rejects_a_type_that_is_not_a_seeder()
    {
        var builder = new ServiceCollection().AddSeeding<Ctx>();

        Assert.Throws<ArgumentException>(() => builder.AddSeeder(typeof(string)));
    }

    [Fact]
    public async Task Options_are_kept_per_context()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<Ctx>();
        services.AddSeeding<Ctx>(o => o.Enabled = false).AddSeeder<FirstA>();
        services.AddSeeding<string>(o => o.FailurePolicy = SeedFailurePolicy.LogAndContinue);
        await using var provider = services.BuildServiceProvider();

        var (_, context) = await RunAsync(provider);

        Assert.Empty(context.Log);
    }

    [Fact]
    public async Task RunSeedersAsync_resolves_the_context_from_a_new_scope()
    {
        await using var provider = Build(b => b.AddSeeder<FirstA>());

        var result = await provider.RunSeedersAsync<Ctx>();

        Assert.Equal("FirstA", Assert.Single(result.Seeders).Name);
    }

    [Fact]
    public async Task Without_configured_files_the_default_content_folder_is_used()
    {
        await using var provider = Build(_ => { });

        var files = provider.GetRequiredService<ISeedFileSource<Ctx>>();

        Assert.Contains(Path.Combine("Seeding", "Files"), files.Description, StringComparison.Ordinal);
        Assert.Null(await files.TryOpenReadAsync("nothing.json", CancellationToken.None));
    }
}
