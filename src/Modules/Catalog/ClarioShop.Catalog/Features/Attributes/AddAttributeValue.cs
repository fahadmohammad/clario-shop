using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;

namespace ClarioShop.Catalog.Features.Attributes;

/// <summary>Adds a value (say, XXL) after an attribute's existing ones.</summary>
public static class AddAttributeValue
{
    public sealed record Request(string Value);

    public sealed record Command(Guid AttributeId, Request Value);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() => RuleFor(request => request.Value).NotEmpty().MaximumLength(50);
    }

    internal sealed class Handler(CatalogDbContext db) : ICommandHandler<Command, AttributeItem>
    {
        public async Task<Result<AttributeItem>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var attribute = await db.LoadAttributeAsync(command.AttributeId, cancellationToken);
            if (attribute is null)
            {
                return CatalogErrors.AttributeNotFound;
            }

            var added = attribute.AddValue(command.Value.Value);
            if (added.IsFailure)
            {
                return added.Error;
            }

            var saved = await db.SaveAttributeAsync(attribute, command.Value.Value, cancellationToken);
            return saved.IsFailure ? saved.Error : AttributeItem.From(attribute);
        }
    }
}
