using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;

namespace ClarioShop.Catalog.Features.Attributes;

public static class RenameAttribute
{
    public sealed record Request(string Name);

    public sealed record Command(Guid AttributeId, Request Attribute);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() => RuleFor(request => request.Name).NotEmpty().MaximumLength(50);
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

            var name = command.Attribute.Name;
            if (await db.AttributeNameTakenAsync(name, attribute.Id, cancellationToken))
            {
                return CatalogErrors.AttributeNameTaken(name.Trim());
            }

            attribute.Rename(name);
            var saved = await db.SaveAttributeAsync(attribute, null, cancellationToken);
            return saved.IsFailure ? saved.Error : AttributeItem.From(attribute);
        }
    }
}
