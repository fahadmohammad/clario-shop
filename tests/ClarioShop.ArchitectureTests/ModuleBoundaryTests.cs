using System.Reflection;
using ClarioShop.Catalog;
using ClarioShop.Catalog.Contracts;
using ClarioShop.Identity;
using ClarioShop.Identity.Contracts;
using ClarioShop.Infrastructure.Endpoints;
using ClarioShop.Inventory;
using ClarioShop.Inventory.Contracts;
using ClarioShop.Platform;
using ClarioShop.SharedKernel.Results;
using NetArchTest.Rules;

namespace ClarioShop.ArchitectureTests;

/// <summary>Enforces the module rules in CLAUDE.md: modules see each other only through .Contracts.</summary>
public sealed class ModuleBoundaryTests
{
    private static readonly Dictionary<string, Assembly> Modules = new()
    {
        ["ClarioShop.Catalog"] = typeof(CatalogModule).Assembly,
        ["ClarioShop.Identity"] = typeof(IdentityModule).Assembly,
        ["ClarioShop.Inventory"] = typeof(InventoryModule).Assembly,
        ["ClarioShop.Platform"] = typeof(PlatformModule).Assembly,
    };

    private static readonly Dictionary<string, Assembly> Contracts = new()
    {
        ["ClarioShop.Catalog.Contracts"] = typeof(ICatalogQueries).Assembly,
        ["ClarioShop.Identity.Contracts"] = typeof(SystemRoles).Assembly,
        ["ClarioShop.Inventory.Contracts"] = typeof(IInventoryReservations).Assembly,
    };

    public static TheoryData<string, string> ModulePairs()
    {
        var data = new TheoryData<string, string>();
        foreach (var module in Modules.Keys)
        {
            foreach (var other in Modules.Keys.Where(other => other != module))
            {
                data.Add(module, other);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ModulePairs))]
    public void Module_DoesNotDependOnAnotherModulesInternals(string module, string other)
    {
        var result = Types.InAssembly(Modules[module])
            .ShouldNot()
            .HaveDependencyOnAny(InternalNamespacesOf(other))
            .GetResult();

        Assert.True(result.IsSuccessful, $"{module} uses internals of {other}: {Describe(result)}");
    }

    [Fact]
    public void SharedKernel_DependsOnNoInfrastructureOrModules()
    {
        var result = Types.InAssembly(typeof(Result).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "ClarioShop.Infrastructure",
                "ClarioShop.Catalog",
                "ClarioShop.Identity",
                "ClarioShop.Inventory",
                "ClarioShop.Platform")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Infrastructure_DependsOnNoModule()
    {
        var result = Types.InAssembly(typeof(IEndpointGroup).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("ClarioShop.Catalog", "ClarioShop.Identity", "ClarioShop.Inventory", "ClarioShop.Platform")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Theory]
    [InlineData("ClarioShop.Catalog")]
    [InlineData("ClarioShop.Identity")]
    [InlineData("ClarioShop.Inventory")]
    [InlineData("ClarioShop.Platform")]
    public void ModuleDomain_DoesNotDependOnPersistenceOrWeb(string module)
    {
        var result = Types.InAssembly(Modules[module])
            .That().ResideInNamespace($"{module}.Domain")
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", $"{module}.Persistence", $"{module}.Features")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Theory]
    [InlineData("ClarioShop.Catalog")]
    [InlineData("ClarioShop.Identity")]
    [InlineData("ClarioShop.Inventory")]
    [InlineData("ClarioShop.Platform")]
    public void ModuleDomainAndPersistence_AreNotPublic(string module)
    {
        var result = Types.InAssembly(Modules[module])
            .That().ResideInNamespaceMatching($@"^{module.Replace(".", @"\.", StringComparison.Ordinal)}\.(Domain|Persistence|Services)")
            .And().DoNotResideInNamespaceContaining("Migrations")
            .Should().NotBePublic()
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Theory]
    [MemberData(nameof(ModulePairs))]
    public void Module_ReferencesOnlyContractsAssemblyOfAnotherModule(string module, string other)
    {
        var references = Modules[module].GetReferencedAssemblies().Select(reference => reference.Name).ToList();

        Assert.DoesNotContain(other, references);
    }

    [Theory]
    [MemberData(nameof(ContractsAssemblies))]
    public void Contracts_DependOnNothingButSharedKernel(string contracts)
    {
        var result = Types.InAssembly(Contracts[contracts])
            .ShouldNot()
            .HaveDependencyOnAny(
            [
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "ClarioShop.Infrastructure",
                .. Modules.Keys.SelectMany(InternalNamespacesOf),
            ])
            .GetResult();

        Assert.True(result.IsSuccessful, $"{contracts}: {Describe(result)}");
    }

    public static TheoryData<string> ContractsAssemblies() => new(Contracts.Keys);

    private static string[] InternalNamespacesOf(string module) =>
        [$"{module}.Domain", $"{module}.Persistence", $"{module}.Features", $"{module}.Services"];

    private static string Describe(NetArchTest.Rules.TestResult result) =>
        result.IsSuccessful ? string.Empty : string.Join(", ", result.FailingTypeNames ?? []);
}
