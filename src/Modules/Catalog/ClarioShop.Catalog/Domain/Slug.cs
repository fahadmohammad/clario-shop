using System.Text;

namespace ClarioShop.Catalog.Domain;

/// <summary>URL slugs from Latin letters and digits. Names written only in Bangla fall back to a generated slug.</summary>
internal static class Slug
{
    public static string From(string text, string fallbackPrefix)
    {
        var builder = new StringBuilder(text.Length);
        var lastWasHyphen = true;

        foreach (var character in text.Trim().ToLowerInvariant())
        {
            if (character is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                builder.Append(character);
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen)
            {
                builder.Append('-');
                lastWasHyphen = true;
            }
        }

        var slug = builder.ToString().Trim('-');
        if (slug.Length > 120)
        {
            slug = slug[..120].TrimEnd('-');
        }

        return slug.Length > 0 ? slug : $"{fallbackPrefix}-{Guid.CreateVersion7().ToString("N")[^8..]}";
    }
}
