namespace ClarioShop.Catalog.Features.Products;

internal static class LikePattern
{
    /// <summary>An ILIKE pattern matching <paramref name="text"/> anywhere, with its wildcards escaped by a backslash.</summary>
    public static string Containing(string text) =>
        "%" + text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal) + "%";
}
