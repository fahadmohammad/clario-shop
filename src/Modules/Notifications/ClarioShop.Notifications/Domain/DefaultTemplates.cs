namespace ClarioShop.Notifications.Domain;

/// <summary>
/// The built-in messages in Bangla and English. SMS stay short (one Unicode SMS holds 70 characters, so Bangla
/// messages take two or three parts); emails are plain text.
/// </summary>
internal static class DefaultTemplates
{
    public const string Bangla = "bn";

    public const string English = "en";

    private static readonly Dictionary<(OrderNotificationKind, NotificationChannel, string), MessageTemplate> Templates = new()
    {
        [(OrderNotificationKind.OrderPlaced, NotificationChannel.Sms, English)] = new(null,
            "{StoreName}: Thank you! We received your order {OrderNumber}, total {Total}. We will call you to confirm it."),
        [(OrderNotificationKind.OrderPlaced, NotificationChannel.Sms, Bangla)] = new(null,
            "{StoreName}: ধন্যবাদ! আপনার অর্ডার {OrderNumber} পেয়েছি, মোট {Total}। নিশ্চিত করতে আমরা আপনাকে কল করব।"),
        [(OrderNotificationKind.OrderPlaced, NotificationChannel.Email, English)] = new(
            "{StoreName}: we received your order {OrderNumber}",
            "Dear {CustomerName},\n\nThank you for your order {OrderNumber}. The total is {Total}. We will call you shortly to confirm it.\n\n{StoreName}"),
        [(OrderNotificationKind.OrderPlaced, NotificationChannel.Email, Bangla)] = new(
            "{StoreName}: আপনার অর্ডার {OrderNumber} পেয়েছি",
            "প্রিয় {CustomerName},\n\nআপনার অর্ডার {OrderNumber}-এর জন্য ধন্যবাদ। মোট {Total}। নিশ্চিত করতে আমরা শীঘ্রই আপনাকে কল করব।\n\n{StoreName}"),

        [(OrderNotificationKind.OrderConfirmed, NotificationChannel.Sms, English)] = new(null,
            "{StoreName}: Your order {OrderNumber} is confirmed. We are getting it ready."),
        [(OrderNotificationKind.OrderConfirmed, NotificationChannel.Sms, Bangla)] = new(null,
            "{StoreName}: আপনার অর্ডার {OrderNumber} নিশ্চিত হয়েছে। আমরা এটি প্রস্তুত করছি।"),
        [(OrderNotificationKind.OrderConfirmed, NotificationChannel.Email, English)] = new(
            "{StoreName}: your order {OrderNumber} is confirmed",
            "Dear {CustomerName},\n\nYour order {OrderNumber} is confirmed and we are getting it ready. We will let you know when it is on its way.\n\n{StoreName}"),
        [(OrderNotificationKind.OrderConfirmed, NotificationChannel.Email, Bangla)] = new(
            "{StoreName}: আপনার অর্ডার {OrderNumber} নিশ্চিত হয়েছে",
            "প্রিয় {CustomerName},\n\nআপনার অর্ডার {OrderNumber} নিশ্চিত হয়েছে, আমরা এটি প্রস্তুত করছি। পাঠানোর সময় আপনাকে জানাব।\n\n{StoreName}"),

        [(OrderNotificationKind.OrderShipped, NotificationChannel.Sms, English)] = new(null,
            "{StoreName}: Your order {OrderNumber} is on its way."),
        [(OrderNotificationKind.OrderShipped, NotificationChannel.Sms, Bangla)] = new(null,
            "{StoreName}: আপনার অর্ডার {OrderNumber} পাঠানো হয়েছে।"),
        [(OrderNotificationKind.OrderShipped, NotificationChannel.Email, English)] = new(
            "{StoreName}: your order {OrderNumber} is on its way",
            "Dear {CustomerName},\n\nYour order {OrderNumber} has left our shop and is on its way to you.\n\n{StoreName}"),
        [(OrderNotificationKind.OrderShipped, NotificationChannel.Email, Bangla)] = new(
            "{StoreName}: আপনার অর্ডার {OrderNumber} পাঠানো হয়েছে",
            "প্রিয় {CustomerName},\n\nআপনার অর্ডার {OrderNumber} আমাদের দোকান থেকে আপনার কাছে পাঠানো হয়েছে।\n\n{StoreName}"),

        [(OrderNotificationKind.OrderDelivered, NotificationChannel.Sms, English)] = new(null,
            "{StoreName}: Your order {OrderNumber} has been delivered. Thank you for shopping with us!"),
        [(OrderNotificationKind.OrderDelivered, NotificationChannel.Sms, Bangla)] = new(null,
            "{StoreName}: আপনার অর্ডার {OrderNumber} পৌঁছে গেছে। আমাদের সাথে কেনাকাটার জন্য ধন্যবাদ!"),
        [(OrderNotificationKind.OrderDelivered, NotificationChannel.Email, English)] = new(
            "{StoreName}: your order {OrderNumber} has been delivered",
            "Dear {CustomerName},\n\nYour order {OrderNumber} has been delivered. Thank you for shopping with us!\n\n{StoreName}"),
        [(OrderNotificationKind.OrderDelivered, NotificationChannel.Email, Bangla)] = new(
            "{StoreName}: আপনার অর্ডার {OrderNumber} পৌঁছে গেছে",
            "প্রিয় {CustomerName},\n\nআপনার অর্ডার {OrderNumber} পৌঁছে গেছে। আমাদের সাথে কেনাকাটার জন্য ধন্যবাদ!\n\n{StoreName}"),
    };

    /// <summary>The language messages are written in: <paramref name="language"/> when there are templates in it, English otherwise.</summary>
    public static string LanguageFor(string? language) =>
        string.Equals(language, Bangla, StringComparison.OrdinalIgnoreCase) ? Bangla : English;

    /// <param name="language">A language from <see cref="LanguageFor"/>.</param>
    public static MessageTemplate For(OrderNotificationKind kind, NotificationChannel channel, string language) =>
        Templates[(kind, channel, language)];

    /// <summary>The stand-in for <see cref="MessagePlaceholders.CustomerName"/> when the shopper gave no name.</summary>
    public static string UnnamedCustomer(string language) => language == Bangla ? "গ্রাহক" : "Customer";
}
