namespace ClarioShop.Infrastructure.Sms;

/// <summary>Sends a text message. The gateway is not chosen yet; until it is, messages are only logged.</summary>
public interface ISmsSender
{
    /// <param name="phone">E.164, e.g. <c>+8801712345678</c>.</param>
    Task SendAsync(string phone, string message, CancellationToken cancellationToken);
}
