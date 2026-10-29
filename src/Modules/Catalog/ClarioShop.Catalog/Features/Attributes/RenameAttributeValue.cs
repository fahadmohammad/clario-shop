using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;

namespace ClarioShop.Catalog.Features.Attributes;

/// <summary>Renames a value. Variants keep pointing at it, so the storefront shows the new spelling.</summary>
public static class RenameAttributeValue
{
    public sealed record Request(string Value);

    public sealed record Command(Guid AttributeId, Guid ValueId, Request Value);

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

            var renamed = attribute.RenameValue(command.ValueId, command.Value.Value);
            if (renamed.IsFailure)
            {
                return renamed.Error;
            }

            var saved = await db.SaveAttributeAsync(attribute, command.Value.Value, cancellationToken);
            return saved.IsFailure ? saved.Error : AttributeItem.From(attribute);
        }
    }
}
