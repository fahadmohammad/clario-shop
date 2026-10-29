namespace ClarioShop.Notifications.Domain;

/// <summary>
/// The built-in stock alerts to the store's contact, in Bangla and English. Unlike the shopper's messages a store
/// cannot replace them with its own text.
/// </summary>
internal static class StaffAlertTemplates
{
    private static readonly Dictionary<(NotificationKind, NotificationChannel, string), MessageTemplate> Templates = new()
    {
        [(NotificationKind.StockWentNegative, NotificationChannel.Sms, DefaultTemplates.English)] = new(null,
            "{StoreName}: Stock of {ProductName} ({Sku}) at {LocationCode} is below zero ({OnHand}). A sale was recorded without stock; please count it."),
        [(NotificationKind.StockWentNegative, NotificationChannel.Sms, DefaultTemplates.Bangla)] = new(null,
            "{StoreName}: {LocationCode}-এ {ProductName} ({Sku})-এর স্টক শূন্যের নিচে ({OnHand})। স্টক ছাড়া বিক্রি রেকর্ড হয়েছে; অনুগ্রহ করে গুনে দেখুন।"),
        [(NotificationKind.StockWentNegative, NotificationChannel.Email, DefaultTemplates.English)] = new(
            "{StoreName}: {Sku} stock is below zero at {LocationCode}",
            "The stock of {ProductName} ({Sku}) at {LocationCode} is now {OnHand}, below zero. A sale was recorded that the stock records had no stock for, so the shelf count or an earlier receipt is wrong.\n\nPlease count it and record an adjustment.\n\n{StoreName}"),
        [(NotificationKind.StockWentNegative, NotificationChannel.Email, DefaultTemplates.Bangla)] = new(
            "{StoreName}: {LocationCode}-এ {Sku}-এর স্টক শূন্যের নিচে",
            "{LocationCode}-এ {ProductName} ({Sku})-এর স্টক এখন {OnHand}, শূন্যের নিচে। স্টক না থাকা অবস্থায় একটি বিক্রি রেকর্ড হয়েছে, তাই তাকের হিসাব বা আগের কোনো গ্রহণ ভুল।\n\nঅনুগ্রহ করে গুনে একটি সমন্বয় রেকর্ড করুন।\n\n{StoreName}"),

        [(NotificationKind.StockDriftDetected, NotificationChannel.Sms, DefaultTemplates.English)] = new(null,
            "{StoreName}: The stock check corrected stock counts that did not match the stock records, e.g. {Sku} at {LocationCode}. See the drift report."),
        [(NotificationKind.StockDriftDetected, NotificationChannel.Sms, DefaultTemplates.Bangla)] = new(null,
            "{StoreName}: স্টক যাচাইয়ে রেকর্ডের সাথে না মেলা স্টকের হিসাব ঠিক করা হয়েছে, যেমন {LocationCode}-এ {Sku}। ড্রিফট রিপোর্ট দেখুন।"),
        [(NotificationKind.StockDriftDetected, NotificationChannel.Email, DefaultTemplates.English)] = new(
            "{StoreName}: the stock check corrected stock counts",
            "The stock check found stock counts that did not match the stock records and set them back to the records. For example, {Sku} at {LocationCode} showed {RecordedOnHand} on hand and {RecordedReserved} reserved; the records hold {LedgerOnHand} on hand and {HeldReserved} reserved.\n\nThis points at a software fault or a manual change to the database. Every correction is listed in the drift report under Inventory.\n\n{StoreName}"),
        [(NotificationKind.StockDriftDetected, NotificationChannel.Email, DefaultTemplates.Bangla)] = new(
            "{StoreName}: স্টক যাচাইয়ে স্টকের হিসাব ঠিক করা হয়েছে",
            "স্টক যাচাইয়ে কিছু স্টকের হিসাব রেকর্ডের সাথে মেলেনি, তাই রেকর্ড অনুযায়ী ঠিক করা হয়েছে। যেমন, {LocationCode}-এ {Sku}-এর হাতে থাকা স্টক দেখাচ্ছিল {RecordedOnHand} ও সংরক্ষিত {RecordedReserved}; রেকর্ডে আছে হাতে {LedgerOnHand} ও সংরক্ষিত {HeldReserved}।\n\nএটি সফটওয়্যারের ত্রুটি বা ডাটাবেসে হাতে করা পরিবর্তন নির্দেশ করে। সব সংশোধন ইনভেন্টরির ড্রিফট রিপোর্টে আছে।\n\n{StoreName}"),
    };

    /// <summary>Every stock alert kind, the kinds the store's contact is told about.</summary>
    public static IReadOnlyList<NotificationKind> Kinds { get; } = [.. Templates.Keys.Select(key => key.Item1).Distinct()];

    /// <param name="language">A language from <see cref="DefaultTemplates.LanguageFor"/>.</param>
    public static MessageTemplate For(NotificationKind kind, NotificationChannel channel, string language) =>
        Templates[(kind, channel, language)];
}
