namespace ClarioShop.Infrastructure.Messaging;

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    public string Exchange { get; set; } = "clarioshop.events";

    public string DeadLetterExchange { get; set; } = "clarioshop.dead";

    public TimeSpan OutboxPollInterval { get; set; } = TimeSpan.FromSeconds(2);

    public int OutboxBatchSize { get; set; } = 50;

    public int MaxAttempts { get; set; } = 10;
}
