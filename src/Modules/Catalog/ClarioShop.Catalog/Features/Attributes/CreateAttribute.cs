using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;

namespace ClarioShop.Catalog.Features.Attributes;

public static class CreateAttribute
{
    public sealed record Command(string Name, IReadOnlyList<string>? Values);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Name).NotEmpty().MaximumLength(50);
            RuleFor(command => command.Values).Must(values => values is null || values.Count <= 100)
                .WithMessage("An attribute can have at most 100 values.");
            RuleForEach(command => command.Values).NotEmpty().MaximumLength(50);
        }
    }

    internal sealed class Handler(CatalogDbContext db, TimeProvider clock) : ICommandHandler<Command, AttributeItem>
    {
        public async Task<Result<AttributeItem>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            if (await db.AttributeNameTakenAsync(command.Name, null, cancellationToken))
            {
                return CatalogErrors.AttributeNameTaken(command.Name.Trim());
            }

            var created = ProductAttribute.Create(command.Name, command.Values ?? [], clock.GetUtcNow());
            if (created.IsFailure)
            {
                return created.Error;
            }

            var attribute = created.Value;
            db.Attributes.Add(attribute);
            var saved = await db.SaveAttributeAsync(attribute, null, cancellationToken);
            return saved.IsFailure ? saved.Error : AttributeItem.From(attribute);
        }
    }
}
