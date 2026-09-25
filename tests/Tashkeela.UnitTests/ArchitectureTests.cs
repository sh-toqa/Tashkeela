using System.Reflection;
using Tashkeela.Application;
using Tashkeela.Domain.Teams;

namespace Tashkeela.UnitTests;

/// <summary>The few structural rules worth enforcing automatically: dependencies point inward.</summary>
public sealed class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Team).Assembly;
    private static readonly Assembly Application = typeof(DependencyInjection).Assembly;

    [Fact]
    public void Domain_references_only_the_base_class_library()
    {
        Domain.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ShouldAllBe(name => name.StartsWith("System", StringComparison.Ordinal) || name == "netstandard");
    }

    [Fact]
    public void Application_does_not_reference_Infrastructure_the_database_provider_or_ASP_NET_Core()
    {
        Application.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ShouldAllBe(name => name != "Tashkeela.Infrastructure"
                && !name.StartsWith("Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal)
                && !name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Domain_entities_expose_no_public_setters()
    {
        var settable = Domain.GetTypes()
            .Where(t => t.IsClass && !t.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)))
            .Where(t => !t.GetMethods().Any(m => m.Name == "<Clone>$")) // records (parameter objects) are immutable by design
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(p => p.SetMethod?.IsPublic == true)
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}");

        settable.ShouldBeEmpty();
    }
}
