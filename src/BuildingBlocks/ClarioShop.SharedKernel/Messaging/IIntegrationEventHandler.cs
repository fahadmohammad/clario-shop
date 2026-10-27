namespace ClarioShop.SharedKernel.Messaging;

/// <summary>Reacts to an integration event inside the tenant the event belongs to. Runs at most once per event.</summary>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}
