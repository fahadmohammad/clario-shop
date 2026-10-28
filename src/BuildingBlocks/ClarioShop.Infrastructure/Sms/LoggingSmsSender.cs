namespace ClarioShop.Infrastructure.Sms;

/// <summary>
/// Stand-in for a real SMS gateway. Phone numbers and message text (which can hold sign-in codes) are logged at
/// Debug only, so they stay out of production logs.
/// </summary>
internal sealed partial class LoggingSmsSender(ILogger<LoggingSmsSender> logger) : ISmsSender
{
    public Task SendAsync(string phone, string message, CancellationToken cancellationToken)
    {
        LogSent(logger, message.Length);
        LogContent(logger, phone, message);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SMS of {Length} characters handed to the logging sender; no gateway is configured")]
    private static partial void LogSent(ILogger logger, int length);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SMS to {Phone}: {Message}")]
    private static partial void LogContent(ILogger logger, string phone, string message);
}
