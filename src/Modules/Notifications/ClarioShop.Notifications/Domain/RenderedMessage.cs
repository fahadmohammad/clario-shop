namespace ClarioShop.Notifications.Domain;

/// <param name="Subject">Email only; null for SMS.</param>
internal sealed record RenderedMessage(string? Subject, string Body);
