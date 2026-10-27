using FluentValidation;

namespace ClarioShop.Infrastructure.Endpoints;

internal sealed class ValidationFilter<TRequest>(IValidator<TRequest>? validator = null) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (validator is null || request is null)
        {
            return await next(context);
        }

        var validation = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        return validation.IsValid
            ? await next(context)
            : TypedResults.ValidationProblem(validation.ToDictionary());
    }
}
