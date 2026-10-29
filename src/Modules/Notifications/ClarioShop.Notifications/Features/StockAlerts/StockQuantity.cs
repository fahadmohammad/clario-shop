using System.Globalization;

namespace ClarioShop.Notifications.Features.StockAlerts;

/// <summary>A stock quantity as staff read it: <c>-2</c>, <c>1.5</c> (kg), never <c>-2.000</c>.</summary>
internal static class StockQuantity
{
    public static string Format(decimal quantity) => quantity.ToString("0.###", CultureInfo.InvariantCulture);
}
