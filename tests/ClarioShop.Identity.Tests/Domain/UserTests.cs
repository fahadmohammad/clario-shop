using ClarioShop.Identity.Domain;

namespace ClarioShop.Identity.Tests.Domain;

public sealed class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);

    private static readonly Role Clerk = Role.Create("Inventory Clerk", isSystem: true);

    private static readonly Role Catalog = Role.Create("Catalog Manager", isSystem: true);

    private static readonly Role Orders = Role.Create("Order Handler", isSystem: true);

    [Fact]
    public void SetRoles_DifferentRoles_ReplacesThemAndReportsChange()
    {
        var user = StaffWith(Clerk, Catalog);

        var changed = user.SetRoles([Catalog, Orders]);

        Assert.True(changed);
        Assert.Equal(new HashSet<Guid> { Catalog.Id, Orders.Id }, user.Roles.Select(r => r.RoleId).ToHashSet());
    }

    [Fact]
    public void SetRoles_SameRolesInAnotherOrder_ReportsNoChange()
    {
        var user = StaffWith(Clerk, Catalog);

        var changed = user.SetRoles([Catalog, Clerk]);

        Assert.False(changed);
        Assert.Equal(2, user.Roles.Count);
    }

    [Fact]
    public void SetRoles_DuplicateRole_AssignsItOnce()
    {
        var user = StaffWith(Clerk);

        user.SetRoles([Orders, Orders]);

        Assert.Equal(Orders.Id, Assert.Single(user.Roles).RoleId);
    }

    private static User StaffWith(params Role[] roles)
    {
        var user = User.Create("staff@shop.test", "Staff", Now);
        foreach (var role in roles)
        {
            user.AssignRole(role);
        }

        return user;
    }
}
