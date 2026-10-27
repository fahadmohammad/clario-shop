namespace ClarioShop.Infrastructure.Persistence;

/// <summary>A tenant-scoped module DbContext, used by migrations and the outbox processor.</summary>
public sealed record ModuleDbContextRegistration(Type ContextType, string Schema);
