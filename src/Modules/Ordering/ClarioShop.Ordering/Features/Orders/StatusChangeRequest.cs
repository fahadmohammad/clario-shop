namespace ClarioShop.Ordering.Features.Orders;

/// <summary>Optional body of a status change: why, e.g. "Customer confirmed by phone" or a cancellation reason.</summary>
public sealed record StatusChangeRequest(string? Note)
{
    internal sealed class Validator : AbstractValidator<StatusChangeRequest>
    {
        public Validator()
        {
            RuleFor(request => request.Note).MaximumLength(500);
        }
    }
}
