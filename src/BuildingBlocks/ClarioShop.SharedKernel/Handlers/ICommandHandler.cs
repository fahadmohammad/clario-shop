using ClarioShop.SharedKernel.Results;

namespace ClarioShop.SharedKernel.Handlers;

/// <summary>Handles one command or query of a vertical slice.</summary>
public interface ICommandHandler<in TCommand, TResponse>
{
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
