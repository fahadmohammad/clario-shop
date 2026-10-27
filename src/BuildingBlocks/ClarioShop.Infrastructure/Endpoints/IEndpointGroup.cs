namespace ClarioShop.Infrastructure.Endpoints;

/// <summary>One per resource. Discovered by <see cref="EndpointRegistration.MapEndpoints"/>; never map endpoints in Program.cs.</summary>
public interface IEndpointGroup
{
    void Map(IEndpointRouteBuilder app);
}
