using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Identity.Contracts;

/// <summary>Sets up identity for a new tenant. Must be called inside that tenant's scope.</summary>
public interface IIdentityProvisioning
{
    Task<Result> CreateOwnerAsync(string email, string fullName, string password, CancellationToken cancellationToken);
}
