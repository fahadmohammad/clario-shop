namespace ClarioShop.Infrastructure.Tenancy;

public sealed record TenantInfo(Guid Id, string Identifier, string Name, string ConnectionString, bool IsActive);
