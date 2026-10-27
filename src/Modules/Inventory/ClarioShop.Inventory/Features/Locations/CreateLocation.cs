using ClarioShop.Inventory.Domain;
using ClarioShop.Inventory.Persistence;

namespace ClarioShop.Inventory.Features.Locations;

public static class CreateLocation
{
    public sealed record Command(string Name, string Code, string? Address);

    public sealed record Response(Guid Id, string Name, string Code, bool IsDefault);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
            RuleFor(command => command.Code).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9-]+$")
                .WithMessage("Code may contain letters, digits and dashes.");
            RuleFor(command => command.Address).MaximumLength(500);
        }
    }

    internal sealed class Handler(InventoryDbContext db, TimeProvider clock) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var code = Location.NormalizeCode(command.Code);
            if (await db.Locations.AnyAsync(l => l.Code == code, cancellationToken))
            {
                return Error.Conflict("inventory.location_code_taken", $"Location code {code} is already used.");
            }

            var location = Location.Create(command.Name, code, command.Address, clock.GetUtcNow());
            db.Locations.Add(location);
            await db.SaveChangesAsync(cancellationToken);
            return new Response(location.Id, location.Name, location.Code, location.IsDefault);
        }
    }
}
