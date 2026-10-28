namespace ClarioShop.Infrastructure.Email;

/// <summary>
/// Stand-in for a real email provider. Addresses and content are logged at Debug only, so they stay out of
/// production logs.
/// </summary>
internal sealed partial class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        LogSent(logger, body.Length);
        LogContent(logger, to, subject, body);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email of {Length} characters handed to the logging sender; no provider is configured")]
    private static partial void LogSent(ILogger logger, int length);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Email to {To}, subject {Subject}: {Body}")]
    private static partial void LogContent(ILogger logger, string to, string subject, string body);
}
