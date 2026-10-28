using System.Reflection;
using ClarioShop.Catalog;
using ClarioShop.Catalog.Contracts;
using ClarioShop.Customers;
using ClarioShop.Customers.Contracts;
using ClarioShop.Identity;
using ClarioShop.Identity.Contracts;
using ClarioShop.Infrastructure.Endpoints;
using ClarioShop.Inventory;
using ClarioShop.Inventory.Contracts;
using ClarioShop.Notifications;
using ClarioShop.Notifications.Contracts;
using ClarioShop.Ordering;
using ClarioShop.Ordering.Contracts;
using ClarioShop.Payments;
using ClarioShop.Payments.Contracts;
using ClarioShop.Platform;
using ClarioShop.Reporting;
using ClarioShop.Reporting.Contracts;
using ClarioShop.Settings;
using ClarioShop.Settings.Contracts;
using ClarioShop.SharedKernel.Results;
using NetArchTest.Rules;

namespace ClarioShop.ArchitectureTests;

/// <summary>Enforces the module rules in CLAUDE.md: modules see each other only through .Contracts.</summary>
public sealed class ModuleBoundaryTests
{
    private static readonly Dictionary<string, Assembly> Modules = new()
    {
        ["ClarioShop.Catalog"] = typeof(CatalogModule).Assembly,
        ["ClarioShop.Customers"] = typeof(CustomersModule).Assembly,
        ["ClarioShop.Identity"] = typeof(IdentityModule).Assembly,
        ["ClarioShop.Inventory"] = typeof(InventoryModule).Assembly,
        ["ClarioShop.Notifications"] = typeof(NotificationsModule).Assembly,
        ["ClarioShop.Ordering"] = typeof(OrderingModule).Assembly,
        ["ClarioShop.Payments"] = typeof(PaymentsModule).Assembly,
        ["ClarioShop.Platform"] = typeof(PlatformModule).Assembly,
        ["ClarioShop.Reporting"] = typeof(ReportingModule).Assembly,
        ["ClarioShop.Settings"] = typeof(SettingsModule).Assembly,
    };

    private static readonly Dictionary<string, Assembly> Contracts = new()
    {
        ["ClarioShop.Catalog.Contracts"] = typeof(ICatalogQueries).Assembly,
        ["ClarioShop.Customers.Contracts"] = typeof(ICustomerDirectory).Assembly,
        ["ClarioShop.Identity.Contracts"] = typeof(SystemRoles).Assembly,
        ["ClarioShop.Inventory.Contracts"] = typeof(IInventoryReservations).Assembly,
        ["ClarioShop.Notifications.Contracts"] = typeof(NotificationsPermissions).Assembly,
        ["ClarioShop.Ordering.Contracts"] = typeof(OrderingPermissions).Assembly,
        ["ClarioShop.Payments.Contracts"] = typeof(PaymentsPermissions).Assembly,
        ["ClarioShop.Reporting.Contracts"] = typeof(ReportingPermissions).Assembly,
        ["ClarioShop.Settings.Contracts"] = typeof(ISettingsQueries).Assembly,
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
                "ClarioShop.Customers",
                "ClarioShop.Identity",
                "ClarioShop.Inventory",
                "ClarioShop.Notifications",
                "ClarioShop.Ordering",
                "ClarioShop.Payments",
                "ClarioShop.Platform",
                "ClarioShop.Reporting",
                "ClarioShop.Settings")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Infrastructure_DependsOnNoModule()
    {
        var result = Types.InAssembly(typeof(IEndpointGroup).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("ClarioShop.Catalog", "ClarioShop.Customers", "ClarioShop.Identity", "ClarioShop.Inventory", "ClarioShop.Notifications", "ClarioShop.Ordering", "ClarioShop.Payments", "ClarioShop.Platform", "ClarioShop.Reporting", "ClarioShop.Settings")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Theory]
    [InlineData("ClarioShop.Catalog")]
    [InlineData("ClarioShop.Customers")]
    [InlineData("ClarioShop.Identity")]
    [InlineData("ClarioShop.Inventory")]
    [InlineData("ClarioShop.Notifications")]
    [InlineData("ClarioShop.Ordering")]
    [InlineData("ClarioShop.Payments")]
    [InlineData("ClarioShop.Platform")]
    [InlineData("ClarioShop.Reporting")]
    [InlineData("ClarioShop.Settings")]
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
    [InlineData("ClarioShop.Customers")]
    [InlineData("ClarioShop.Identity")]
    [InlineData("ClarioShop.Inventory")]
    [InlineData("ClarioShop.Notifications")]
    [InlineData("ClarioShop.Ordering")]
    [InlineData("ClarioShop.Payments")]
    [InlineData("ClarioShop.Platform")]
    [InlineData("ClarioShop.Reporting")]
    [InlineData("ClarioShop.Settings")]
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
