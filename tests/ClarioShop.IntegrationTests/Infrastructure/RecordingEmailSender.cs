using System.Collections.Concurrent;
using ClarioShop.Infrastructure.Email;

namespace ClarioShop.IntegrationTests.Infrastructure;

/// <summary>Keeps every email the API sends.</summary>
public sealed class RecordingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<SentEmail> sent = new();

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        sent.Enqueue(new SentEmail(to, subject, body));
        return Task.CompletedTask;
    }

    /// <summary>Emails sent to the address, oldest first.</summary>
    public IReadOnlyList<SentEmail> To(string address) => sent.Where(email => email.To == address).ToList();
}

public sealed record SentEmail(string To, string Subject, string Body);
