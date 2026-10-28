using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using ClarioShop.Infrastructure.Sms;

namespace ClarioShop.IntegrationTests.Infrastructure;

/// <summary>Keeps every SMS the API sends, so tests can read sign-in codes.</summary>
public sealed partial class RecordingSmsSender : ISmsSender
{
    private readonly ConcurrentQueue<(string Phone, string Message)> sent = new();

    public Task SendAsync(string phone, string message, CancellationToken cancellationToken)
    {
        sent.Enqueue((phone, message));
        return Task.CompletedTask;
    }

    public int CountTo(string phone) => sent.Count(sms => sms.Phone == phone);

    /// <summary>The 6-digit code in the latest SMS to the phone (E.164).</summary>
    public string LatestCodeTo(string phone)
    {
        var message = sent.Last(sms => sms.Phone == phone).Message;
        return SixDigits().Match(message).Value;
    }

    [GeneratedRegex(@"\b[0-9]{6}\b")]
    private static partial Regex SixDigits();
}
