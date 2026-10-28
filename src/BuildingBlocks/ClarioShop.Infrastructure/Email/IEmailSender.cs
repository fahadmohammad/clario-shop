namespace ClarioShop.Infrastructure.Email;

/// <summary>Sends an email. The provider is not chosen yet; until it is, emails are only logged.</summary>
public interface IEmailSender
{
    /// <param name="body">Plain text.</param>
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken);
}
