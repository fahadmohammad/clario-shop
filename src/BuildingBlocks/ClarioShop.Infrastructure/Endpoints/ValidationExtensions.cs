namespace ClarioShop.Infrastructure.Endpoints;

public static class ValidationExtensions
{
    /// <summary>Runs the FluentValidation validator for <typeparamref name="TRequest"/> before the handler.</summary>
    public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<ValidationFilter<TRequest>>().ProducesValidationProblem();
}
