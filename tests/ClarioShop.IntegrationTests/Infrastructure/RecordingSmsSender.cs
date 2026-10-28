using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using ClarioShop.Infrastructure.Sms;

namespace ClarioShop.IntegrationTests.Infrastructure;

/// <summary>Keeps every SMS the API sends, so tests can read sign-in codes and order notifications.</summary>
public sealed partial class RecordingSmsSender : ISmsSender
{
    private readonly ConcurrentQueue<(string Phone, string Message)> sent = new();
    private readonly ConcurrentDictionary<string, int> failuresLeft = new();

    public Task SendAsync(string phone, string message, CancellationToken cancellationToken)
    {
        if (failuresLeft.TryGetValue(phone, out var left) && left > 0 && failuresLeft.TryUpdate(phone, left - 1, left))
        {
            throw new HttpRequestException("SMS gateway unavailable (test)");
        }

        sent.Enqueue((phone, message));
        return Task.CompletedTask;
    }

    public int CountTo(string phone) => sent.Count(sms => sms.Phone == phone);

    /// <summary>Messages sent to the phone (E.164) whose text contains <paramref name="text"/>, oldest first.</summary>
    public IReadOnlyList<string> MessagesTo(string phone, string text) =>
        sent.Where(sms => sms.Phone == phone && sms.Message.Contains(text, StringComparison.Ordinal)).Select(sms => sms.Message).ToList();

    /// <summary>The next <paramref name="times"/> messages to the phone (E.164) fail, as if the gateway were down.</summary>
    public void FailNext(string phone, int times) => failuresLeft[phone] = times;

    /// <summary>
    /// The 6-digit code in the latest sign-in SMS to the phone (E.164). Order notifications to the same phone, from
    /// other tests, are skipped.
    /// </summary>
    public string LatestCodeTo(string phone)
    {
        var message = sent.Last(sms => sms.Phone == phone && sms.Message.Contains("sign-in code", StringComparison.Ordinal)).Message;
        return SixDigits().Match(message).Value;
    }

    [GeneratedRegex(@"\b[0-9]{6}\b")]
    private static partial Regex SixDigits();
}
