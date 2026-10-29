using ClarioShop.SharedKernel.Tenancy;

namespace ClarioShop.Infrastructure.Tenancy;

/// <param name="Plan">The plan's name, for messages (e.g. "Online").</param>
/// <param name="Features">What the plan includes.</param>
public sealed record TenantInfo(
    Guid Id,
    string Identifier,
    string Name,
    string ConnectionString,
    bool IsActive,
    string Plan,
    PlanFeatures Features);
