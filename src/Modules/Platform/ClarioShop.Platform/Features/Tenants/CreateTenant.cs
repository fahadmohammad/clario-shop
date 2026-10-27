using ClarioShop.Platform.Services;

namespace ClarioShop.Platform.Features.Tenants;

public static class CreateTenant
{
    public sealed record Command(
        string Identifier,
        string Name,
        IReadOnlyList<string> Hosts,
        string OwnerEmail,
        string OwnerName,
        string OwnerPassword);

    public sealed record Response(Guid Id, string Identifier, string Name);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Identifier).NotEmpty().Matches("^[a-z][a-z0-9-]{2,39}$")
                .WithMessage("Use 3–40 lowercase letters, digits or dashes, starting with a letter.");
            RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
            RuleFor(command => command.Hosts).NotEmpty();
            RuleForEach(command => command.Hosts).NotEmpty().MaximumLength(253)
                .Must(host => Uri.CheckHostName(host.Trim()) != UriHostNameType.Unknown).WithMessage("'{PropertyValue}' is not a valid host name.");
            RuleFor(command => command.OwnerEmail).NotEmpty().EmailAddress();
            RuleFor(command => command.OwnerName).NotEmpty().MaximumLength(200);
            RuleFor(command => command.OwnerPassword).NotEmpty().MinimumLength(8)
                .Matches("[A-Za-z]").WithMessage("Password must contain a letter.")
                .Matches("[0-9]").WithMessage("Password must contain a digit.");
        }
    }

    internal sealed class Handler(TenantProvisioner provisioner) : ICommandHandler<Command, Response>
    {
        public async Task<Result<Response>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var result = await provisioner.ProvisionAsync(
                new TenantRequest(command.Identifier, command.Name, command.Hosts, command.OwnerEmail, command.OwnerName, command.OwnerPassword),
                cancellationToken);

            return result.IsSuccess
                ? new Response(result.Value.Id, result.Value.Identifier, result.Value.Name)
                : result.Error;
        }
    }
}
