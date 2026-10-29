using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KitStack.Seeding;

/// <summary>Registers the seeders of <typeparamref name="TContext"/>. Returned by <c>AddSeeding&lt;TContext&gt;()</c>.</summary>
/// <typeparam name="TContext">The context the seeders write to.</typeparam>
public sealed class SeedingBuilder<TContext>
    where TContext : class
{
    internal SeedingBuilder(IServiceCollection services) => Services = services;

    /// <summary>The service collection.</summary>
    public IServiceCollection Services { get; }

    /// <summary>Registers <typeparamref name="TSeeder"/> (transient). Registering the same type twice has no effect.</summary>
    public SeedingBuilder<TContext> AddSeeder<TSeeder>()
        where TSeeder : class, ISeeder<TContext>
        => AddSeeder(typeof(TSeeder));

    /// <summary>Registers <paramref name="seederType"/> (transient). Registering the same type twice has no effect.</summary>
    public SeedingBuilder<TContext> AddSeeder(Type seederType)
    {
        ArgumentNullException.ThrowIfNull(seederType);
        if (!IsSeeder(seederType))
            throw new ArgumentException($"{seederType.FullName} is not a concrete ISeeder<{typeof(TContext).Name}>.", nameof(seederType));

        Services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(ISeeder<TContext>), seederType));
        return this;
    }

    /// <summary>
    /// Registers every concrete, non-generic class in <paramref name="assembly"/> that implements
    /// <see cref="ISeeder{TContext}"/>. Abstract bases (per-module shims) are skipped.
    /// </summary>
    public SeedingBuilder<TContext> AddSeedersFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        foreach (var type in GetLoadableTypes(assembly).Where(IsSeeder).OrderBy(t => t.FullName, StringComparer.Ordinal))
            AddSeeder(type);
        return this;
    }

    /// <summary>Registers the seeders of the assembly that contains <typeparamref name="TMarker"/>.</summary>
    public SeedingBuilder<TContext> AddSeedersFromAssemblyContaining<TMarker>()
        => AddSeedersFromAssembly(typeof(TMarker).Assembly);

    private static bool IsSeeder(Type type)
        => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false }
           && typeof(ISeeder<TContext>).IsAssignableFrom(type);

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
